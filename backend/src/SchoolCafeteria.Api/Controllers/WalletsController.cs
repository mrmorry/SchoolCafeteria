using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/wallets")]
public class WalletsController : ApiControllerBase
{
    private readonly WalletService _walletService;
    private readonly IAppDbContext _db;

    public WalletsController(WalletService walletService, IAppDbContext db)
    {
        _walletService = walletService;
        _db = db;
    }

    [HttpGet("by-buyer/{buyerId:guid}")]
    public async Task<ActionResult<WalletDto>> GetByBuyer(Guid buyerId, CancellationToken ct)
    {
        await EnsureCanAccessBuyerAsync(buyerId, ct);
        var wallet = await _walletService.GetByBuyerIdAsync(buyerId, ct);
        return wallet is null ? NotFound() : Ok(wallet);
    }

    [HttpGet("{walletId:guid}/transactions")]
    public async Task<ActionResult<PagedResult<WalletTransactionDto>>> GetTransactions(Guid walletId, [FromQuery] PagedRequest request, CancellationToken ct)
    {
        await EnsureCanAccessWalletAsync(walletId, ct);
        return Ok(await _walletService.GetTransactionsAsync(walletId, request, ct));
    }

    [HttpGet("{walletId:guid}/last-purchases")]
    public async Task<ActionResult<IReadOnlyList<WalletTransactionDto>>> GetLastPurchases(Guid walletId, CancellationToken ct)
    {
        await EnsureCanAccessWalletAsync(walletId, ct);
        return Ok(await _walletService.GetLastPurchasesAsync(walletId, 5, ct));
    }

    [HttpPut("{walletId:guid}/low-balance-threshold")]
    public async Task<IActionResult> SetThreshold(Guid walletId, SetLowBalanceThresholdRequest request, CancellationToken ct)
    {
        await EnsureCanAccessWalletAsync(walletId, ct);
        await _walletService.SetLowBalanceThresholdAsync(walletId, request, ct);
        return NoContent();
    }

    [HttpPost("adjust")]
    [RequirePermission("wallets.adjust")]
    public async Task<ActionResult<WalletTransactionDto>> ManualAdjustment(ManualAdjustmentRequest request, CancellationToken ct)
        => Ok(await _walletService.ManualAdjustmentAsync(SchoolId, request, UserId, ct));

    /// <summary>Enforces "tutor solo ve estudiantes asociados" and "estudiante solo ve su propia cartera" at the API boundary.</summary>
    private async Task EnsureCanAccessBuyerAsync(Guid buyerId, CancellationToken ct)
    {
        if (CurrentUser.HasPermission("wallets.read")) return; // staff

        if (CurrentUser.BuyerId is { } ownBuyerId && ownBuyerId == buyerId) return; // self-service buyer portal

        if (CurrentUser.GuardianId is { } guardianId)
        {
            var student = await _db.Students.FirstOrDefaultAsync(s => s.BuyerId == buyerId, ct);
            if (student is not null)
            {
                var linked = await _db.GuardianStudents.AnyAsync(gs => gs.GuardianId == guardianId && gs.StudentId == student.Id, ct);
                if (linked) return;
            }
        }

        throw new ForbiddenException("No tiene acceso a la cartera de este comprador.");
    }

    private async Task EnsureCanAccessWalletAsync(Guid walletId, CancellationToken ct)
    {
        var wallet = await _db.Wallets.FirstOrDefaultAsync(w => w.Id == walletId, ct) ?? throw new NotFoundException("Wallet", walletId);
        await EnsureCanAccessBuyerAsync(wallet.BuyerId, ct);
    }
}
