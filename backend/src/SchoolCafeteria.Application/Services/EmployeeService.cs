using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

public class EmployeeService
{
    private readonly IAppDbContext _db;
    public EmployeeService(IAppDbContext db) => _db = db;

    public async Task<EmployeeDto> CreateAsync(Guid schoolId, CreateEmployeeRequest request, CancellationToken ct = default)
    {
        var duplicate = await _db.Employees.AnyAsync(e => e.SchoolId == schoolId && e.EmployeeCode == request.EmployeeCode && !e.IsDeleted, ct);
        if (duplicate)
            throw new BusinessRuleException("employee.duplicate_code", $"Ya existe un empleado con código '{request.EmployeeCode}'.");

        var school = await _db.Schools.FirstAsync(s => s.Id == schoolId, ct);
        var buyer = new Buyer { SchoolId = schoolId, Type = request.EmployeeType == "Teacher" ? BuyerType.Teacher : BuyerType.AdminEmployee, FullName = request.FullName };
        _db.Buyers.Add(buyer);
        _db.Wallets.Add(new Wallet { SchoolId = schoolId, BuyerId = buyer.Id, Currency = school.DefaultCurrency, Balance = 0 });

        var employee = new Employee
        {
            SchoolId = schoolId, BuyerId = buyer.Id, EmployeeCode = request.EmployeeCode,
            FullName = request.FullName, Email = request.Email, EmployeeType = request.EmployeeType,
            Status = EmployeeStatus.Active
        };
        _db.Employees.Add(employee);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(schoolId, employee.Id, ct) ?? throw new NotFoundException(nameof(Employee), employee.Id);
    }

    public async Task<PagedResult<EmployeeDto>> SearchAsync(Guid schoolId, PagedRequest request, CancellationToken ct = default)
    {
        var query = _db.Employees.Where(e => e.SchoolId == schoolId && !e.IsDeleted);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(e => e.FullName.Contains(term) || e.EmployeeCode.Contains(term));
        }
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(e => e.FullName).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var dtos = new List<EmployeeDto>();
        foreach (var e in items) dtos.Add(await MapAsync(e, ct));
        return new PagedResult<EmployeeDto>(dtos, total, request.Page, request.PageSize);
    }

    public async Task<EmployeeDto?> GetByIdAsync(Guid schoolId, Guid id, CancellationToken ct = default)
    {
        var e = await _db.Employees.FirstOrDefaultAsync(x => x.Id == id && x.SchoolId == schoolId, ct);
        return e is null ? null : await MapAsync(e, ct);
    }

    private async Task<EmployeeDto> MapAsync(Employee e, CancellationToken ct)
    {
        var wallet = await _db.Wallets.FirstAsync(w => w.BuyerId == e.BuyerId, ct);
        var hasRfid = await _db.RfidCredentials.AnyAsync(c => c.BuyerId == e.BuyerId && c.Status == RfidCredentialStatus.Active, ct);
        return new EmployeeDto(e.Id, e.EmployeeCode, e.FullName, e.Email, e.EmployeeType, e.Status, e.BuyerId, wallet.Id, wallet.Balance, hasRfid);
    }
}
