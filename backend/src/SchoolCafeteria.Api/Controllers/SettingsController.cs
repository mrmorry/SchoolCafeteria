using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/settings")]
public class SettingsController : ApiControllerBase
{
    private readonly SettingsService _service;
    public SettingsController(SettingsService service) => _service = service;

    [HttpGet]
    [RequirePermission("settings.write")]
    public async Task<ActionResult<IReadOnlyList<SystemSetting>>> GetAll(CancellationToken ct)
        => Ok(await _service.GetAllAsync(SchoolId, ct));

    [HttpPut("{key}")]
    [RequirePermission("settings.write")]
    public async Task<IActionResult> Set(string key, [FromBody] SetSettingRequest request, CancellationToken ct)
    {
        await _service.SetAsync(SchoolId, key, request.Value, request.ValueType, request.Description, ct);
        return NoContent();
    }
}

public record SetSettingRequest(string Value, string ValueType, string? Description);
