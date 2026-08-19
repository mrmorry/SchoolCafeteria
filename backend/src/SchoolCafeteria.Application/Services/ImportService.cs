using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>
/// Handles the manual CSV student import flow: template -> preview -> validate -> confirm.
/// Re-running the same file is idempotent because rows are matched by the natural key
/// (StudentCode) rather than blindly inserted.
/// </summary>
public class ImportService
{
    private static readonly string[] TemplateHeaders =
        { "StudentCode", "FirstName", "LastName", "Level", "Section", "StudentEmail", "GuardianFullName", "GuardianEmail", "GuardianPhone" };

    private readonly IAppDbContext _db;
    private readonly StudentService _studentService;
    private readonly NotificationOutboxService _notifications;

    public ImportService(IAppDbContext db, StudentService studentService, NotificationOutboxService notifications)
    {
        _db = db;
        _studentService = studentService;
        _notifications = notifications;
    }

    public static string BuildCsvTemplate() => string.Join(',', TemplateHeaders) + "\n" +
        "S-1001,Ana,Gómez,Primaria,3A,,Carlos Gómez,carlos.gomez@example.com,+50760000000\n";

    public async Task<ImportJobDto> UploadAndValidateAsync(Guid schoolId, string fileName, string csvContent, ImportMode mode, string userId, CancellationToken ct = default)
    {
        var lines = csvContent.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
            throw new BusinessRuleException("import.empty_file", "El archivo está vacío.");

        var header = lines[0].Split(',').Select(h => h.Trim()).ToArray();
        var job = new ImportJob { SchoolId = schoolId, FileName = fileName, EntityType = "Student", Mode = mode, Status = ImportJobStatus.Validating, ExecutedByUserId = userId };
        _db.ImportJobs.Add(job);

        var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingCodes = (await _db.Students.Where(s => s.SchoolId == schoolId).Select(s => s.StudentCode).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int valid = 0, errors = 0, duplicates = 0;

        for (var i = 1; i < lines.Length; i++)
        {
            var cols = lines[i].Split(',');
            var record = new Dictionary<string, string>();
            for (var c = 0; c < header.Length && c < cols.Length; c++) record[header[c]] = cols[c].Trim();

            record.TryGetValue("StudentCode", out var code);
            record.TryGetValue("FirstName", out var firstName);
            record.TryGetValue("LastName", out var lastName);
            record.TryGetValue("GuardianEmail", out var guardianEmail);

            string? error = null;
            var status = ImportRowStatus.Valid;

            if (string.IsNullOrWhiteSpace(code)) error = "StudentCode es obligatorio.";
            else if (string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName)) error = "Nombre y apellido son obligatorios.";
            else if (string.IsNullOrWhiteSpace(guardianEmail)) error = "El correo del tutor es obligatorio.";
            else if (!seenCodes.Add(code))
            {
                status = ImportRowStatus.Duplicate;
                error = "Código duplicado dentro del mismo archivo.";
            }
            else if (existingCodes.Contains(code) && mode == ImportMode.Create)
            {
                status = ImportRowStatus.Duplicate;
                error = "El estudiante ya existe (modo Create no permite actualizarlo).";
            }

            if (error is not null && status != ImportRowStatus.Duplicate) status = ImportRowStatus.Error;

            if (status == ImportRowStatus.Valid) valid++;
            else if (status == ImportRowStatus.Duplicate) duplicates++;
            else errors++;

            _db.ImportJobRows.Add(new ImportJobRow
            {
                ImportJobId = job.Id, RowNumber = i, RawDataJson = JsonSerializer.Serialize(record),
                Status = status, ErrorMessage = error, NaturalKey = code
            });
        }

        job.TotalRows = lines.Length - 1;
        job.ValidRows = valid;
        job.ErrorRows = errors;
        job.DuplicateRows = duplicates;
        job.Status = ImportJobStatus.Validated;
        await _db.SaveChangesAsync(ct);

        return ToDto(job);
    }

    public async Task<IReadOnlyList<ImportPreviewRowDto>> GetPreviewAsync(Guid importJobId, CancellationToken ct = default)
    {
        var rows = await _db.ImportJobRows.Where(r => r.ImportJobId == importJobId).OrderBy(r => r.RowNumber).ToListAsync(ct);
        return rows.Select(r => new ImportPreviewRowDto(r.RowNumber, r.NaturalKey ?? string.Empty, r.Status.ToString(), r.ErrorMessage, r.RawDataJson)).ToList();
    }

    /// <summary>Idempotent by StudentCode: re-confirming the same job (or re-uploading the same file) never
    /// duplicates a student — rows already imported are skipped.</summary>
    public async Task<ImportJobDto> ConfirmAsync(Guid schoolId, Guid importJobId, string userId, CancellationToken ct = default)
    {
        var job = await _db.ImportJobs.Include(j => j.Rows).FirstOrDefaultAsync(j => j.Id == importJobId && j.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(ImportJob), importJobId);

        job.Status = ImportJobStatus.Importing;
        await _db.SaveChangesAsync(ct);

        var imported = 0;
        foreach (var row in job.Rows.Where(r => r.Status is ImportRowStatus.Valid))
        {
            var record = JsonSerializer.Deserialize<Dictionary<string, string>>(row.RawDataJson) ?? new();
            var existing = await _db.Students.FirstOrDefaultAsync(s => s.SchoolId == schoolId && s.StudentCode == row.NaturalKey, ct);

            if (existing is null && job.Mode != ImportMode.Deactivate)
            {
                await _studentService.CreateAsync(schoolId, new CreateStudentRequest(
                    row.NaturalKey!, record.GetValueOrDefault("FirstName", ""), record.GetValueOrDefault("LastName", ""),
                    null, null, EmptyToNull(record.GetValueOrDefault("StudentEmail")),
                    record.GetValueOrDefault("GuardianFullName", ""), record.GetValueOrDefault("GuardianEmail", ""),
                    EmptyToNull(record.GetValueOrDefault("GuardianPhone"))), userId, ct);
                row.Status = ImportRowStatus.Imported;
                imported++;
            }
            else if (existing is not null && job.Mode == ImportMode.CreateOrUpdate)
            {
                existing.FirstName = record.GetValueOrDefault("FirstName", existing.FirstName);
                existing.LastName = record.GetValueOrDefault("LastName", existing.LastName);
                row.Status = ImportRowStatus.Imported;
                imported++;
            }
            else if (existing is not null && job.Mode == ImportMode.Deactivate)
            {
                existing.Status = StudentStatus.Inactive;
                row.Status = ImportRowStatus.Imported;
                imported++;
            }
            else
            {
                row.Status = ImportRowStatus.Skipped;
            }
        }

        job.ImportedRows = imported;
        job.Status = ImportJobStatus.Completed;
        job.CompletedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _notifications.EnqueueAsync(schoolId, NotificationEvent.ImportCompleted, NotificationChannel.InApp, userId,
            "Importación completada", $"Se importaron {imported} estudiantes desde '{job.FileName}'.",
            Guid.NewGuid().ToString(), $"ImportCompleted:{job.Id}", ct);

        return ToDto(job);
    }

    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static ImportJobDto ToDto(ImportJob j) => new(j.Id, j.FileName, j.Status.ToString(), j.TotalRows, j.ValidRows,
        j.ErrorRows, j.DuplicateRows, j.ImportedRows, j.CreatedAtUtc, j.CompletedAtUtc, j.ResultFileUrl);
}
