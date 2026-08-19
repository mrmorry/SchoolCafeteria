using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Application.Services;

/// <summary>Server-side paginated, filterable reports. Dates are stored UTC and returned as UTC —
/// the client renders them in the school's configured time zone.</summary>
public class ReportService
{
    private readonly IAppDbContext _db;
    public ReportService(IAppDbContext db) => _db = db;

    public async Task<PagedResult<RechargeReportRow>> GetRechargesAsync(Guid schoolId, ReportFilter filter, CancellationToken ct = default)
    {
        var query = from t in _db.WalletTransactions
                     join w in _db.Wallets on t.WalletId equals w.Id
                     join b in _db.Buyers on w.BuyerId equals b.Id
                     where t.SchoolId == schoolId && t.Type == WalletTransactionType.Recharge
                           && t.OccurredAtUtc >= filter.FromUtc && t.OccurredAtUtc <= filter.ToUtc
                     orderby t.OccurredAtUtc descending
                     select new RechargeReportRow(t.OccurredAtUtc, t.TransactionNumber, b.FullName, t.Amount,
                         t.Channel.ToString(), t.PaymentMethod!.ToString()!, t.PerformedByUserId);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<RechargeReportRow>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<PagedResult<SalesReportRow>> GetSalesAsync(Guid schoolId, ReportFilter filter, CancellationToken ct = default)
    {
        var query = from s in _db.Sales
                     join b in _db.Buyers on s.BuyerId equals b.Id
                     join p in _db.PointsOfSale on s.PointOfSaleId equals p.Id
                     where s.SchoolId == schoolId && s.OccurredAtUtc >= filter.FromUtc && s.OccurredAtUtc <= filter.ToUtc
                           && (filter.PointOfSaleId == null || s.PointOfSaleId == filter.PointOfSaleId)
                     orderby s.OccurredAtUtc descending
                     select new SalesReportRow(s.OccurredAtUtc, s.SaleNumber, b.FullName, p.Name, s.OperatorUserId, s.Subtotal, s.TaxTotal, s.Total);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((filter.Page - 1) * filter.PageSize).Take(filter.PageSize).ToListAsync(ct);
        return new PagedResult<SalesReportRow>(items, total, filter.Page, filter.PageSize);
    }

    public async Task<IReadOnlyList<LowStockReportRow>> GetLowStockAsync(Guid schoolId, CancellationToken ct = default)
    {
        var query = from b in _db.InventoryBalances
                     join w in _db.Warehouses on b.WarehouseId equals w.Id
                     join p in _db.Products on b.ProductId equals p.Id
                     where w.SchoolId == schoolId && b.QuantityOnHand <= p.MinStockLevel
                     select new LowStockReportRow(p.Code, p.Name, w.Name, b.QuantityOnHand, p.MinStockLevel);
        return await query.ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CashDifferenceReportRow>> GetCashDifferencesAsync(Guid schoolId, ReportFilter filter, CancellationToken ct = default)
    {
        var query = from s in _db.RegisterShifts
                     join r in _db.Registers on s.RegisterId equals r.Id
                     where s.SchoolId == schoolId && s.Status == Domain.Enums.ShiftStatus.Closed
                           && s.ClosedAtUtc >= filter.FromUtc && s.ClosedAtUtc <= filter.ToUtc && s.CashDifference != 0
                     orderby s.ClosedAtUtc descending
                     select new CashDifferenceReportRow(s.ClosedAtUtc!.Value, r.Name, s.OperatorUserId, s.ExpectedCash ?? 0, s.ClosingCounted ?? 0, s.CashDifference ?? 0);
        return await query.ToListAsync(ct);
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(Guid schoolId, CancellationToken ct = default)
    {
        var todayStart = DateTime.UtcNow.Date;
        var todaySales = await _db.Sales.Where(s => s.SchoolId == schoolId && s.OccurredAtUtc >= todayStart && s.Status == Domain.Enums.SaleStatus.Completed)
            .SumAsync(s => (decimal?)s.Total, ct) ?? 0;
        var todayRecharges = await _db.WalletTransactions
            .Where(t => t.SchoolId == schoolId && t.Type == WalletTransactionType.Recharge && t.OccurredAtUtc >= todayStart)
            .SumAsync(t => (decimal?)t.Amount, ct) ?? 0;
        var todayTransactions = await _db.WalletTransactions.CountAsync(t => t.SchoolId == schoolId && t.OccurredAtUtc >= todayStart, ct);
        var lowStock = (await GetLowStockAsync(schoolId, ct)).Count;
        var activeWallets = await _db.Wallets.CountAsync(w => w.SchoolId == schoolId && w.Status == Domain.Enums.WalletStatus.Active, ct);
        var totalBalance = await _db.Wallets.Where(w => w.SchoolId == schoolId).SumAsync(w => (decimal?)w.Balance, ct) ?? 0;

        return new DashboardSummaryDto(todaySales, todayRecharges, todayTransactions, lowStock, activeWallets, totalBalance);
    }
}
