using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/guardians")]
public class GuardiansController : ApiControllerBase
{
    private readonly GuardianService _service;
    private readonly StudentService _studentService;
    public GuardiansController(GuardianService service, StudentService studentService)
    {
        _service = service;
        _studentService = studentService;
    }

    [HttpPost]
    [RequirePermission("guardians.write")]
    public async Task<ActionResult<GuardianDto>> Create(CreateGuardianRequest request, CancellationToken ct)
        => Ok(await _service.CreateAsync(SchoolId, request, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("guardians.read")]
    public async Task<ActionResult<GuardianDto>> GetById(Guid id, CancellationToken ct)
    {
        var guardian = await _service.GetByIdAsync(SchoolId, id, ct);
        return guardian is null ? NotFound() : Ok(guardian);
    }

    [HttpPost("link-student")]
    [RequirePermission("guardians.write")]
    public async Task<IActionResult> LinkStudent(LinkGuardianStudentRequest request, CancellationToken ct)
    {
        await _service.LinkStudentAsync(SchoolId, request, ct);
        return NoContent();
    }

    /// <summary>Staff-facing lookup by id, gated by permission (not the guardian's own portal session).</summary>
    [HttpGet("{id:guid}/students")]
    [RequirePermission("guardians.read")]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> GetStudents(Guid id, CancellationToken ct)
        => Ok(await _service.GetStudentsForGuardianAsync(SchoolId, id, _studentService, ct));

    /// <summary>
    /// Guardian-portal self-service: the linked GuardianId comes only from the caller's own JWT
    /// (set at login from User.GuardianId), never from a client-supplied id — this is what makes
    /// it impossible for a tutor to browse another tutor's students (rule: "tutor solo puede ver
    /// estudiantes asociados").
    /// </summary>
    [HttpGet("me")]
    public async Task<ActionResult<GuardianDto>> GetMe(CancellationToken ct)
    {
        var guardianId = CurrentUser.GuardianId ?? throw new UnauthorizedAccessException("La sesión no está asociada a un tutor.");
        var guardian = await _service.GetByIdAsync(SchoolId, guardianId, ct);
        return guardian is null ? NotFound() : Ok(guardian);
    }

    [HttpGet("me/students")]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> GetMyStudents(CancellationToken ct)
    {
        var guardianId = CurrentUser.GuardianId ?? throw new UnauthorizedAccessException("La sesión no está asociada a un tutor.");
        return Ok(await _service.GetStudentsForGuardianAsync(SchoolId, guardianId, _studentService, ct));
    }
}
