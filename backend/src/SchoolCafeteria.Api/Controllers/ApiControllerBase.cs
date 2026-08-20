using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.Api.Controllers;

[ApiController]
[Authorize]
public abstract class ApiControllerBase : ControllerBase
{
    private ICurrentUserService? _currentUser;
    protected ICurrentUserService CurrentUser => _currentUser ??= HttpContext.RequestServices.GetRequiredService<ICurrentUserService>();

    protected Guid SchoolId => CurrentUser.SchoolId ?? throw new UnauthorizedAccessException("Token sin colegio asociado.");
    protected string UserId => CurrentUser.UserId ?? throw new UnauthorizedAccessException("Token sin usuario asociado.");
}
