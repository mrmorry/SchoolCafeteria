using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/imports")]
public class ImportController : ApiControllerBase
{
    private readonly ImportService _service;
    public ImportController(ImportService service) => _service = service;

    [HttpGet("students/template")]
    [RequirePermission("students.import")]
    public IActionResult DownloadTemplate() =>
        File(System.Text.Encoding.UTF8.GetBytes(ImportService.BuildCsvTemplate()), "text/csv", "plantilla_estudiantes.csv");

    [HttpPost("students/upload")]
    [RequirePermission("students.import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<ActionResult<ImportJobDto>> Upload(IFormFile file, [FromQuery] ImportMode mode, CancellationToken ct)
    {
        using var reader = new StreamReader(file.OpenReadStream());
        var content = await reader.ReadToEndAsync(ct);
        return Ok(await _service.UploadAndValidateAsync(SchoolId, file.FileName, content, mode, UserId, ct));
    }

    [HttpGet("{jobId:guid}/preview")]
    [RequirePermission("students.import")]
    public async Task<ActionResult<IReadOnlyList<ImportPreviewRowDto>>> Preview(Guid jobId, CancellationToken ct)
        => Ok(await _service.GetPreviewAsync(jobId, ct));

    [HttpPost("{jobId:guid}/confirm")]
    [RequirePermission("students.import")]
    public async Task<ActionResult<ImportJobDto>> Confirm(Guid jobId, CancellationToken ct)
        => Ok(await _service.ConfirmAsync(SchoolId, jobId, UserId, ct));
}
