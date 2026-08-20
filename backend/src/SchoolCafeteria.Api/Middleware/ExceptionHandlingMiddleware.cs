using System.Net;
using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Application.Common;

namespace SchoolCafeteria.Api.Middleware;

/// <summary>Translates domain/application exceptions into RFC 7807 ProblemDetails responses with
/// the correct HTTP status code, and logs unexpected errors without leaking internals to the client.</summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var (status, title) = ex switch
            {
                NotFoundException => (HttpStatusCode.NotFound, "Recurso no encontrado"),
                ForbiddenException => (HttpStatusCode.Forbidden, "Operación no autorizada"),
                ConflictException => (HttpStatusCode.Conflict, "Conflicto de concurrencia"),
                BusinessRuleException => (HttpStatusCode.UnprocessableEntity, "Regla de negocio violada"),
                _ => (HttpStatusCode.InternalServerError, "Error interno")
            };

            if (status == HttpStatusCode.InternalServerError)
                _logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);

            var problem = new ProblemDetails
            {
                Status = (int)status,
                Title = title,
                Detail = status == HttpStatusCode.InternalServerError ? "Ocurrió un error inesperado." : ex.Message,
                Instance = context.Request.Path,
                Type = $"https://httpstatuses.io/{(int)status}"
            };
            if (ex is BusinessRuleException bre)
                problem.Extensions["code"] = bre.Code;
            problem.Extensions["correlationId"] = context.TraceIdentifier;

            context.Response.StatusCode = (int)status;
            // WriteAsJsonAsync overwrites Response.ContentType with "application/json" unless the
            // RFC 7807 media type is passed explicitly here.
            await context.Response.WriteAsJsonAsync(problem, options: null, contentType: "application/problem+json");
        }
    }
}
