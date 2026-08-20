using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/inventory")]
public class InventoryController : ApiControllerBase
{
    private readonly InventoryAdminService _service;
    public InventoryController(InventoryAdminService service) => _service = service;

    [HttpGet("warehouses")]
    [RequirePermission("inventory.read")]
    public async Task<ActionResult<IReadOnlyList<WarehouseDto>>> GetWarehouses(CancellationToken ct)
        => Ok(await _service.GetWarehousesAsync(SchoolId, ct));

    [HttpPost("warehouses")]
    [RequirePermission("inventory.write")]
    public async Task<ActionResult<WarehouseDto>> CreateWarehouse(CreateWarehouseRequest request, CancellationToken ct)
        => Ok(await _service.CreateWarehouseAsync(SchoolId, request, ct));

    [HttpGet("balances")]
    [RequirePermission("inventory.read")]
    public async Task<ActionResult<IReadOnlyList<InventoryBalanceDto>>> GetBalances([FromQuery] Guid? warehouseId, [FromQuery] bool lowStockOnly, CancellationToken ct)
        => Ok(await _service.GetBalancesAsync(SchoolId, warehouseId, lowStockOnly, ct));

    [HttpPost("entries")]
    [RequirePermission("inventory.write")]
    public async Task<IActionResult> Entry(InventoryEntryRequest request, CancellationToken ct)
    {
        await _service.EntryAsync(SchoolId, request, UserId, ct);
        return NoContent();
    }

    [HttpPost("adjustments")]
    [RequirePermission("inventory.adjust")]
    public async Task<IActionResult> Adjust(InventoryAdjustmentRequest request, CancellationToken ct)
    {
        await _service.AdjustAsync(SchoolId, request, UserId, ct);
        return NoContent();
    }

    [HttpPost("transfers")]
    [RequirePermission("inventory.write")]
    public async Task<IActionResult> Transfer(InventoryTransferRequest request, CancellationToken ct)
    {
        await _service.TransferAsync(SchoolId, request, UserId, ct);
        return NoContent();
    }
}
