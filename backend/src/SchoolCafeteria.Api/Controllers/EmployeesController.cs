using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/employees")]
public class EmployeesController : ApiControllerBase
{
    private readonly EmployeeService _service;
    public EmployeesController(EmployeeService service) => _service = service;

    [HttpGet]
    [RequirePermission("employees.read")]
    public async Task<ActionResult<PagedResult<EmployeeDto>>> Search([FromQuery] PagedRequest request, CancellationToken ct)
        => Ok(await _service.SearchAsync(SchoolId, request, ct));

    [HttpGet("{id:guid}")]
    [RequirePermission("employees.read")]
    public async Task<ActionResult<EmployeeDto>> GetById(Guid id, CancellationToken ct)
    {
        var employee = await _service.GetByIdAsync(SchoolId, id, ct);
        return employee is null ? NotFound() : Ok(employee);
    }

    [HttpPost]
    [RequirePermission("employees.write")]
    public async Task<ActionResult<EmployeeDto>> Create(CreateEmployeeRequest request, CancellationToken ct)
    {
        var employee = await _service.CreateAsync(SchoolId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = employee.Id }, employee);
    }
}
