using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/recharges")]
public class RechargesController : ApiControllerBase
{
    private readonly RechargeService _service;
    private readonly IAppDbContext _db;
    public RechargesController(RechargeService service, IAppDbContext db)
    {
        _service = service;
        _db = db;
    }

    [HttpPost("presential")]
    [RequirePermission("recharges.create.presential")]
    public async Task<ActionResult<RechargeDto>> Presential(RechargePresentialRequest request, [FromQuery] Guid? pointOfSaleId, [FromQuery] Guid? registerId, CancellationToken ct)
        => Ok(await _service.RechargePresentialAsync(SchoolId, request, UserId, WalletTransactionChannel.CashierOffice, pointOfSaleId, registerId, ct));

    /// <summary>Guardian/student self-service digital recharge. Requires the caller to already be authorized
    /// to access the target wallet — enforced by WalletsController's rules being mirrored here for the buyer check.</summary>
    [HttpPost("digital")]
    public async Task<ActionResult> Digital(RechargeDigitalRequest request, CancellationToken ct)
    {
        if (!CurrentUser.HasPermission("wallets.read")) // staff bypass the ownership check below
        {
            var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == request.WalletId, ct)
                ?? throw new NotFoundException("Wallet", request.WalletId);

            var allowed = CurrentUser.BuyerId == wallet.BuyerId;
            if (!allowed && CurrentUser.GuardianId is { } guardianId)
            {
                var student = await _db.Students.FirstOrDefaultAsync(s => s.BuyerId == wallet.BuyerId, ct);
                allowed = student is not null && await _db.GuardianStudents.AnyAsync(gs => gs.GuardianId == guardianId && gs.StudentId == student.Id, ct);
            }
            if (!allowed) throw new ForbiddenException("No tiene acceso a la cartera de este comprador.");
        }

        var channel = CurrentUser.GuardianId is not null ? WalletTransactionChannel.GuardianPortal : WalletTransactionChannel.StudentPortal;
        var (recharge, checkoutUrl) = await _service.RechargeDigitalAsync(SchoolId, request, UserId, channel, ct);
        return Ok(new { recharge, checkoutUrl });
    }
}
