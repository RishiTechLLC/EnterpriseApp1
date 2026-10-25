using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SchoolCalendar.Application.Common.Exceptions;
using SchoolCalendar.Application.Common.Interfaces;
using SchoolCalendar.Domain.Exceptions;
using SchoolCalendar.Infrastructure.Permify;

namespace SchoolCalendar.Api.Infrastructure;

public sealed class JwtSettings
{
    public const string Section = "Jwt";

    /// <summary>External identity provider. When set, the symmetric dev key is not used.</summary>
    public string? Authority { get; set; }
    public string Issuer { get; set; } = "school-calendar-dev";
    public string Audience { get; set; } = "school-calendar-api";
    public string SigningKey { get; set; } = "";
    /// <summary>Allows /api/auth/dev-token. Never enable outside local development.</summary>
    public bool EnableDevTokens { get; set; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserId => accessor.HttpContext?.User.FindFirst("sub")?.Value;
}

public sealed class GlobalExceptionHandler(IProblemDetailsService problemDetails, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            ValidationException => (StatusCodes.Status400BadRequest, "Validation failed", exception.Message),
            DomainException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", exception.Message),
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            UnauthenticatedException => (StatusCodes.Status401Unauthorized, "Unauthorized", exception.Message),
            ForbiddenAccessException => (StatusCodes.Status403Forbidden, "Forbidden", exception.Message),
            PermifyException or HttpRequestException => (StatusCodes.Status503ServiceUnavailable, "Authorization service unavailable",
                "Access could not be verified. Please try again shortly."),
            _ => (StatusCodes.Status500InternalServerError, "Server error", "An unexpected error occurred."),
        };

        if (status >= 500) logger.LogError(exception, "Unhandled exception");

        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (exception is ValidationException ve) problem.Extensions["errors"] = ve.Errors;

        context.Response.StatusCode = status;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = context, ProblemDetails = problem, Exception = exception });
    }
}
