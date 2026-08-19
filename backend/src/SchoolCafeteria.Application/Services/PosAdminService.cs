using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

public class PosAdminService
{
    private readonly IAppDbContext _db;
    private readonly NotificationOutboxService _notifications;
    private readonly IDateTimeProvider _clock;

    public PosAdminService(IAppDbContext db, NotificationOutboxService notifications, IDateTimeProvider clock)
    {
        _db = db;
        _notifications = notifications;
        _clock = clock;
    }

    public async Task<PointOfSaleDto> CreatePointOfSaleAsync(Guid schoolId, CreatePointOfSaleRequest request, CancellationToken ct = default)
    {
        var pos = new PointOfSale { SchoolId = schoolId, Name = request.Name, Location = request.Location, DefaultWarehouseId = request.DefaultWarehouseId, IsActive = true };
        _db.PointsOfSale.Add(pos);
        await _db.SaveChangesAsync(ct);
        return new PointOfSaleDto(pos.Id, pos.Name, pos.Location, pos.IsActive, Array.Empty<RegisterDto>());
    }

    public async Task<RegisterDto> CreateRegisterAsync(Guid schoolId, CreateRegisterRequest request, CancellationToken ct = default)
    {
        var pos = await _db.PointsOfSale.FirstOrDefaultAsync(p => p.Id == request.PointOfSaleId && p.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(PointOfSale), request.PointOfSaleId);
        var register = new Register { SchoolId = schoolId, PointOfSaleId = pos.Id, Name = request.Name, IsActive = true };
        _db.Registers.Add(register);
        await _db.SaveChangesAsync(ct);
        return new RegisterDto(register.Id, register.Name, register.IsActive);
    }

    public async Task<IReadOnlyList<PointOfSaleDto>> GetPointsOfSaleAsync(Guid schoolId, CancellationToken ct = default)
    {
        var list = await _db.PointsOfSale.Where(p => p.SchoolId == schoolId && !p.IsDeleted).Include(p => p.Registers).ToListAsync(ct);
        return list.Select(p => new PointOfSaleDto(p.Id, p.Name, p.Location, p.IsActive,
            p.Registers.Where(r => !r.IsDeleted).Select(r => new RegisterDto(r.Id, r.Name, r.IsActive)).ToList())).ToList();
    }

    /// <summary>Rule: an operator may only open a shift on a register they are authorized for (enforced by the
    /// API's [RequireRegisterAccess] filter checking UserRole.PointOfSaleId before reaching this service).</summary>
    public async Task<ShiftDto> OpenShiftAsync(Guid schoolId, OpenShiftRequest request, string operatorUserId, CancellationToken ct = default)
    {
        var alreadyOpen = await _db.RegisterShifts.AnyAsync(s => s.RegisterId == request.RegisterId && s.Status == ShiftStatus.Open, ct);
        if (alreadyOpen)
            throw new BusinessRuleException("shift.already_open", "Ya existe un turno abierto para esta caja.");

        var shift = new RegisterShift
        {
            SchoolId = schoolId, RegisterId = request.RegisterId, OperatorUserId = operatorUserId,
            Status = ShiftStatus.Open, OpeningFloat = request.OpeningFloat, OpenedAtUtc = _clock.UtcNow
        };
        _db.RegisterShifts.Add(shift);
        await _db.SaveChangesAsync(ct);
        return await MapAsync(shift, ct);
    }

    public async Task<ShiftDto> CloseShiftAsync(Guid schoolId, CloseShiftRequest request, string performedByUserId, CancellationToken ct = default)
    {
        var shift = await _db.RegisterShifts.Include(s => s.Register)
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId && s.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(RegisterShift), request.ShiftId);
        if (shift.Status != ShiftStatus.Open)
            throw new BusinessRuleException("shift.not_open", "El turno ya está cerrado.");

        var salesCash = await _db.Sales.Where(s => s.RegisterShiftId == shift.Id && s.Status == SaleStatus.Completed).SumAsync(s => (decimal?)s.Total, ct) ?? 0;
        var rechargesCash = await _db.WalletTransactions
            .Where(t => t.RegisterId == shift.RegisterId && t.Type == WalletTransactionType.Recharge
                        && t.PaymentMethod == PaymentMethod.Cash && t.OccurredAtUtc >= shift.OpenedAtUtc)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        shift.ExpectedCash = shift.OpeningFloat + rechargesCash; // cash-only expectation; card/other methods reconcile separately
        shift.ClosingCounted = request.ClosingCounted;
        shift.CashDifference = request.ClosingCounted - shift.ExpectedCash;
        shift.Status = ShiftStatus.Closed;
        shift.ClosedAtUtc = _clock.UtcNow;
        shift.ClosingNotes = request.Notes;
        await _db.SaveChangesAsync(ct);

        if (shift.CashDifference != 0)
        {
            await _notifications.EnqueueAsync(schoolId, NotificationEvent.ShiftClosedWithDifference, NotificationChannel.InApp,
                "supervisors", "Cierre de caja con diferencia",
                $"El turno de {shift.Register!.Name} cerró con una diferencia de {shift.CashDifference:0.00}.",
                Guid.NewGuid().ToString(), $"ShiftDifference:{shift.Id}", ct);
        }

        return await MapAsync(shift, ct, salesCash, rechargesCash);
    }

    private async Task<ShiftDto> MapAsync(RegisterShift s, CancellationToken ct, decimal? salesTotal = null, decimal? rechargesTotal = null)
    {
        var register = s.Register ?? await _db.Registers.FirstAsync(r => r.Id == s.RegisterId, ct);
        salesTotal ??= await _db.Sales.Where(x => x.RegisterShiftId == s.Id && x.Status == SaleStatus.Completed).SumAsync(x => (decimal?)x.Total, ct) ?? 0;
        rechargesTotal ??= await _db.WalletTransactions
            .Where(t => t.RegisterId == s.RegisterId && t.Type == WalletTransactionType.Recharge && t.OccurredAtUtc >= s.OpenedAtUtc)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;

        return new ShiftDto(s.Id, s.RegisterId, register.Name, s.OperatorUserId, s.Status, s.OpeningFloat,
            s.ClosingCounted, s.ExpectedCash, s.CashDifference, s.OpenedAtUtc, s.ClosedAtUtc, salesTotal.Value, rechargesTotal.Value);
    }
}
