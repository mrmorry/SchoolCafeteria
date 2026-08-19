using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/students")]
public class StudentsController : ApiControllerBase
{
    private readonly StudentService _service;
    public StudentsController(StudentService service) => _service = service;

    [HttpGet]
    [RequirePermission("students.read")]
    public async Task<ActionResult<PagedResult<StudentDto>>> Search([FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _service.SearchAsync(SchoolId, request, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("students.read")]
    public async Task<ActionResult<StudentDto>> GetById(Guid id, CancellationToken ct)
    {
        var student = await _service.GetByIdAsync(SchoolId, id, ct);
        return student is null ? NotFound() : Ok(student);
    }

    [HttpPost]
    [RequirePermission("students.write")]
    public async Task<ActionResult<StudentDto>> Create(CreateStudentRequest request, CancellationToken ct)
    {
        var student = await _service.CreateAsync(SchoolId, request, UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = student.Id }, student);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission("students.write")]
    public async Task<ActionResult<StudentDto>> Update(Guid id, UpdateStudentRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(SchoolId, id, request, ct));
}
