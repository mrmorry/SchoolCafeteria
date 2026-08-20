using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

public class GuardianService
{
    private readonly IAppDbContext _db;
    public GuardianService(IAppDbContext db) => _db = db;

    public async Task<GuardianDto> CreateAsync(Guid schoolId, CreateGuardianRequest request, CancellationToken ct = default)
    {
        var guardian = new Guardian { SchoolId = schoolId, FullName = request.FullName, Email = request.Email, Phone = request.Phone };
        _db.Guardians.Add(guardian);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(schoolId, guardian.Id, ct) ?? throw new NotFoundException(nameof(Guardian), guardian.Id);
    }

    public async Task LinkStudentAsync(Guid schoolId, LinkGuardianStudentRequest request, CancellationToken ct = default)
    {
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == request.GuardianId && g.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Guardian), request.GuardianId);
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == request.StudentId && s.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Student), request.StudentId);

        var existing = await _db.GuardianStudents
            .FirstOrDefaultAsync(gs => gs.GuardianId == guardian.Id && gs.StudentId == student.Id, ct);

        if (existing is null)
        {
            _db.GuardianStudents.Add(new GuardianStudent
            {
                GuardianId = guardian.Id, StudentId = student.Id, Relationship = request.Relationship,
                IsPrimary = request.IsPrimary, CanRecharge = request.CanRecharge, CanViewHistory = request.CanViewHistory,
                CanManageRfid = request.CanManageRfid, CanConfigureAlerts = request.CanConfigureAlerts
            });
        }
        else
        {
            existing.Relationship = request.Relationship;
            existing.IsPrimary = request.IsPrimary;
            existing.CanRecharge = request.CanRecharge;
            existing.CanViewHistory = request.CanViewHistory;
            existing.CanManageRfid = request.CanManageRfid;
            existing.CanConfigureAlerts = request.CanConfigureAlerts;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<GuardianDto?> GetByIdAsync(Guid schoolId, Guid guardianId, CancellationToken ct = default)
    {
        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.Id == guardianId && g.SchoolId == schoolId && !g.IsDeleted, ct);
        return guardian is null ? null : await MapAsync(guardian, ct);
    }

    /// <summary>Guardians can only ever see students explicitly linked to them (rule: tutor solo ve estudiantes asociados).</summary>
    public async Task<IReadOnlyList<StudentDto>> GetStudentsForGuardianAsync(Guid schoolId, Guid guardianId, StudentService studentService, CancellationToken ct = default)
    {
        var studentIds = await _db.GuardianStudents.Where(gs => gs.GuardianId == guardianId)
            .Select(gs => gs.StudentId).ToListAsync(ct);

        var result = new List<StudentDto>();
        foreach (var id in studentIds)
        {
            var dto = await studentService.GetByIdAsync(schoolId, id, ct);
            if (dto is not null) result.Add(dto);
        }
        return result;
    }

    private async Task<GuardianDto> MapAsync(Guardian g, CancellationToken ct)
    {
        var links = await _db.GuardianStudents.Where(gs => gs.GuardianId == g.Id).ToListAsync(ct);
        var linkDtos = new List<GuardianStudentLinkDto>();
        foreach (var l in links)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == l.StudentId, ct);
            if (student is null) continue;
            linkDtos.Add(new GuardianStudentLinkDto(student.Id, student.FullName, l.Relationship, l.IsPrimary,
                l.CanRecharge, l.CanViewHistory, l.CanManageRfid, l.CanConfigureAlerts));
        }
        return new GuardianDto(g.Id, g.FullName, g.Email, g.Phone, linkDtos);
    }
}
