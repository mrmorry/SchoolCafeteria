using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/rfid")]
public class RfidController : ApiControllerBase
{
    private readonly RfidService _service;
    public RfidController(RfidService service) => _service = service;

    [HttpPost("issue")]
    [RequirePermission("rfid.manage")]
    public async Task<ActionResult<RfidCredentialDto>> Issue(IssueRfidRequest request, CancellationToken ct)
        => Ok(await _service.IssueAsync(SchoolId, request, UserId, ct));

    [HttpPost("replace")]
    [RequirePermission("rfid.manage")]
    public async Task<ActionResult<RfidCredentialDto>> Replace(ReplaceRfidRequest request, CancellationToken ct)
        => Ok(await _service.ReplaceAsync(SchoolId, request, UserId, ct));

    [HttpPost("block")]
    [RequirePermission("rfid.manage")]
    public async Task<IActionResult> Block(BlockRfidRequest request, CancellationToken ct)
    {
        await _service.BlockAsync(SchoolId, request, UserId, ct);
        return NoContent();
    }

    [HttpPost("unblock")]
    [RequirePermission("rfid.manage")]
    public async Task<IActionResult> Unblock(UnblockRfidRequest request, CancellationToken ct)
    {
        await _service.UnblockAsync(SchoolId, request, UserId, ct);
        return NoContent();
    }

    [HttpPost("report-lost")]
    [RequirePermission("rfid.manage")]
    public async Task<IActionResult> ReportLost(ReportLostRfidRequest request, CancellationToken ct)
    {
        await _service.ReportLostAsync(SchoolId, request, UserId, ct);
        return NoContent();
    }

    /// <summary>POS lookup by UID (keyboard-wedge mode: the reader types this value into the request).</summary>
    [HttpGet("lookup")]
    [RequirePermission("pos.sell")]
    public async Task<ActionResult<RfidLookupResult>> Lookup([FromQuery] string uid, [FromQuery] Guid? pointOfSaleId, CancellationToken ct)
    {
        var result = await _service.LookupAsync(SchoolId, uid, pointOfSaleId, ct);
        return result is null ? NotFound() : Ok(result);
    }
}
