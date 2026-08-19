using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/pos")]
public class PosController : ApiControllerBase
{
    private readonly PosAdminService _posAdmin;
    private readonly SaleService _saleService;

    public PosController(PosAdminService posAdmin, SaleService saleService)
    {
        _posAdmin = posAdmin;
        _saleService = saleService;
    }

    [HttpGet("points-of-sale")]
    [RequirePermission("pos.sell")]
    public async Task<ActionResult<IReadOnlyList<PointOfSaleDto>>> GetPointsOfSale(CancellationToken ct)
        => Ok(await _posAdmin.GetPointsOfSaleAsync(SchoolId, ct));

    [HttpPost("points-of-sale")]
    [RequirePermission("users.manage")]
    public async Task<ActionResult<PointOfSaleDto>> CreatePointOfSale(CreatePointOfSaleRequest request, CancellationToken ct)
        => Ok(await _posAdmin.CreatePointOfSaleAsync(SchoolId, request, ct));

    [HttpPost("registers")]
    [RequirePermission("users.manage")]
    public async Task<ActionResult<RegisterDto>> CreateRegister(CreateRegisterRequest request, CancellationToken ct)
        => Ok(await _posAdmin.CreateRegisterAsync(SchoolId, request, ct));

    [HttpPost("shifts/open")]
    [RequirePermission("pos.shift.manage")]
    public async Task<ActionResult<ShiftDto>> OpenShift(OpenShiftRequest request, CancellationToken ct)
        => Ok(await _posAdmin.OpenShiftAsync(SchoolId, request, UserId, ct));

    [HttpPost("shifts/close")]
    [RequirePermission("pos.shift.manage")]
    public async Task<ActionResult<ShiftDto>> CloseShift(CloseShiftRequest request, CancellationToken ct)
        => Ok(await _posAdmin.CloseShiftAsync(SchoolId, request, UserId, ct));

    /// <summary>Idempotent on (ShiftId, IdempotencyKey): a double click on "Cobrar" returns the same sale.</summary>
    [HttpPost("sales")]
    [RequirePermission("pos.sell")]
    public async Task<ActionResult<SaleDto>> Checkout(CreateSaleRequest request, CancellationToken ct)
        => Ok(await _saleService.CheckoutAsync(SchoolId, request, UserId, ct));

    [HttpPost("sales/cancel")]
    [RequirePermission("pos.refund")]
    public async Task<ActionResult<SaleDto>> Cancel(CancelSaleRequest request, CancellationToken ct)
        => Ok(await _saleService.CancelAsync(SchoolId, request, UserId, ct));
}
