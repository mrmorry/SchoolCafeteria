using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

public class StudentService
{
    private readonly IAppDbContext _db;

    public StudentService(IAppDbContext db) => _db = db;

    /// <summary>Creates the Student, its Buyer and its Wallet (1:1) atomically, plus the primary guardian if new.</summary>
    public async Task<StudentDto> CreateAsync(Guid schoolId, CreateStudentRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var duplicate = await _db.Students.AnyAsync(s => s.SchoolId == schoolId && s.StudentCode == request.StudentCode && !s.IsDeleted, ct);
        if (duplicate)
            throw new BusinessRuleException("student.duplicate_code", $"Ya existe un estudiante con código '{request.StudentCode}'.");

        var school = await _db.Schools.FirstOrDefaultAsync(s => s.Id == schoolId, ct)
            ?? throw new NotFoundException(nameof(Domain.Entities.School), schoolId);

        var buyer = new Buyer { SchoolId = schoolId, Type = BuyerType.Student, FullName = $"{request.FirstName} {request.LastName}".Trim() };
        _db.Buyers.Add(buyer);

        var wallet = new Wallet { SchoolId = schoolId, BuyerId = buyer.Id, Currency = school.DefaultCurrency, Balance = 0 };
        _db.Wallets.Add(wallet);

        var student = new Student
        {
            SchoolId = schoolId,
            BuyerId = buyer.Id,
            StudentCode = request.StudentCode,
            FirstName = request.FirstName,
            LastName = request.LastName,
            SchoolLevelId = request.SchoolLevelId,
            SchoolSectionId = request.SchoolSectionId,
            StudentEmail = request.StudentEmail,
            Status = StudentStatus.Active
        };
        _db.Students.Add(student);

        var guardian = await _db.Guardians.FirstOrDefaultAsync(g => g.SchoolId == schoolId && g.Email == request.GuardianEmail && !g.IsDeleted, ct);
        if (guardian is null)
        {
            guardian = new Guardian { SchoolId = schoolId, FullName = request.GuardianFullName, Email = request.GuardianEmail, Phone = request.GuardianPhone };
            _db.Guardians.Add(guardian);
        }

        _db.GuardianStudents.Add(new GuardianStudent
        {
            GuardianId = guardian.Id, StudentId = student.Id, Relationship = "Parent", IsPrimary = true,
            CanRecharge = true, CanViewHistory = true, CanManageRfid = true, CanConfigureAlerts = true
        });

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(schoolId, student.Id, ct) ?? throw new NotFoundException(nameof(Student), student.Id);
    }

    public async Task<StudentDto> UpdateAsync(Guid schoolId, Guid studentId, UpdateStudentRequest request, CancellationToken ct = default)
    {
        var student = await _db.Students.FirstOrDefaultAsync(s => s.Id == studentId && s.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Student), studentId);

        student.FirstName = request.FirstName;
        student.LastName = request.LastName;
        student.Status = request.Status;
        student.SchoolLevelId = request.SchoolLevelId;
        student.SchoolSectionId = request.SchoolSectionId;
        student.StudentEmail = request.StudentEmail;
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(schoolId, studentId, ct) ?? throw new NotFoundException(nameof(Student), studentId);
    }

    public async Task<StudentDto?> GetByIdAsync(Guid schoolId, Guid studentId, CancellationToken ct = default)
    {
        var s = await Query(schoolId).FirstOrDefaultAsync(x => x.Id == studentId, ct);
        return s is null ? null : await MapAsync(s, ct);
    }

    public async Task<PagedResult<StudentDto>> SearchAsync(Guid schoolId, PagedRequest request, CancellationToken ct = default)
    {
        var query = Query(schoolId);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.StudentCode.Contains(term) || s.FirstName.Contains(term) || s.LastName.Contains(term));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var dtos = new List<StudentDto>();
        foreach (var s in items) dtos.Add(await MapAsync(s, ct));
        return new PagedResult<StudentDto>(dtos, total, request.Page, request.PageSize);
    }

    private IQueryable<Student> Query(Guid schoolId) =>
        _db.Students.Where(s => s.SchoolId == schoolId && !s.IsDeleted)
            .Include(s => s.SchoolLevelRef).Include(s => s.SchoolSectionRef);

    private async Task<StudentDto> MapAsync(Student s, CancellationToken ct)
    {
        var wallet = await _db.Wallets.FirstAsync(w => w.BuyerId == s.BuyerId, ct);
        var hasRfid = await _db.RfidCredentials.AnyAsync(c => c.BuyerId == s.BuyerId && c.Status == RfidCredentialStatus.Active, ct);
        return new StudentDto(s.Id, s.StudentCode, s.FirstName, s.LastName, s.Status,
            s.SchoolLevelId, s.SchoolLevelRef?.Name, s.SchoolSectionId, s.SchoolSectionRef?.Name,
            s.StudentEmail, s.BuyerId, wallet.Id, wallet.Balance, hasRfid, s.CreatedAtUtc, s.UpdatedAtUtc);
    }
}
