using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/reports")]
public class ReportsController : ApiControllerBase
{
    private readonly ReportService _service;
    public ReportsController(ReportService service) => _service = service;

    [HttpGet("dashboard")]
    [RequirePermission("reports.read")]
    public async Task<ActionResult<DashboardSummaryDto>> Dashboard(CancellationToken ct)
        => Ok(await _service.GetDashboardSummaryAsync(SchoolId, ct));

    [HttpGet("recharges")]
    [RequirePermission("reports.read")]
    public async Task<ActionResult<PagedResult<RechargeReportRow>>> Recharges([FromQuery] ReportFilter filter, CancellationToken ct)
        => Ok(await _service.GetRechargesAsync(SchoolId, filter, ct));

    [HttpGet("sales")]
    [RequirePermission("reports.read")]
    public async Task<ActionResult<PagedResult<SalesReportRow>>> Sales([FromQuery] ReportFilter filter, CancellationToken ct)
        => Ok(await _service.GetSalesAsync(SchoolId, filter, ct));

    [HttpGet("low-stock")]
    [RequirePermission("reports.read")]
    public async Task<ActionResult<IReadOnlyList<LowStockReportRow>>> LowStock(CancellationToken ct)
        => Ok(await _service.GetLowStockAsync(SchoolId, ct));

    [HttpGet("cash-differences")]
    [RequirePermission("reports.read")]
    public async Task<ActionResult<IReadOnlyList<CashDifferenceReportRow>>> CashDifferences([FromQuery] ReportFilter filter, CancellationToken ct)
        => Ok(await _service.GetCashDifferencesAsync(SchoolId, filter, ct));

    [HttpGet("recharges/export")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> ExportRecharges([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var data = await _service.GetRechargesAsync(SchoolId, filter with { Page = 1, PageSize = 10000 }, ct);
        var csv = "FechaUtc,Transaccion,Comprador,Monto,Canal,MedioPago,RealizadoPor\n" +
            string.Join('\n', data.Items.Select(r => $"{r.OccurredAtUtc:O},{r.TransactionNumber},{Csv(r.BuyerName)},{r.Amount},{r.Channel},{r.PaymentMethod},{Csv(r.PerformedBy)}"));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "recargas.csv");
    }

    [HttpGet("sales/export")]
    [RequirePermission("reports.export")]
    public async Task<IActionResult> ExportSales([FromQuery] ReportFilter filter, CancellationToken ct)
    {
        var data = await _service.GetSalesAsync(SchoolId, filter with { Page = 1, PageSize = 10000 }, ct);
        var csv = "FechaUtc,Venta,Comprador,PuntoDeVenta,Operador,Subtotal,Impuesto,Total\n" +
            string.Join('\n', data.Items.Select(r => $"{r.OccurredAtUtc:O},{r.SaleNumber},{Csv(r.BuyerName)},{Csv(r.PointOfSale)},{Csv(r.Operator)},{r.Subtotal},{r.Tax},{r.Total}"));
        return File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "ventas.csv");
    }

    private static string Csv(string value) => value.Contains(',') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
