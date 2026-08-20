using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Application.DTOs;

namespace SchoolCafeteria.Api.Controllers;

/// <summary>Read-only by design — there is intentionally no write action anywhere in this controller
/// (rule: un auditor no puede modificar información).</summary>
[Route("api/v1/audit")]
public class AuditController : ApiControllerBase
{
    private readonly AuditService _service;
    public AuditController(AuditService service) => _service = service;

    [HttpGet]
    [RequirePermission("audit.read")]
    public async Task<ActionResult<PagedResult<AuditLog>>> Search(
        [FromQuery] string? entityName, [FromQuery] string? userId, [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
        => Ok(await _service.SearchAsync(SchoolId, entityName, userId, fromUtc, toUtc, page, pageSize, ct));
}
