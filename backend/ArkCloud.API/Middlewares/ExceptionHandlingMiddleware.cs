using ArkCloud.Application.Exceptions;
using ArkCloud.Domain.Exceptions;
using FluentValidation;
using System.Text.Json;

namespace ArkCloud.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            _logger.LogWarning(ex, "Validation failed for {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            // ex.Message is FluentValidation's raw dump ("Validation failed: -- Password: ...
            // Severity: Error"), not fit for end users. Build a clean sentence from the actual
            // error messages, and also expose them grouped by field (ASP.NET Core's usual
            // ValidationProblemDetails.errors shape) for clients that want per-field display.
            var errorsByField = ex.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

            var payload = new
            {
                title = "Validation failed",
                status = 400,
                detail = string.Join(" ", ex.Errors.Select(e => e.ErrorMessage)),
                errors = errorsByField,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (AuthenticationException ex)
        {
            _logger.LogWarning(ex, "Authentication failed for {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                title = "Authentication failed",
                status = 401,
                detail = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (NotFoundException ex)
        {
            _logger.LogInformation(ex, "Resource not found for {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                title = "Resource not found",
                status = 404,
                detail = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (DomainException ex)
        {
            _logger.LogWarning(ex, "Business rule violation for {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                title = "Business rule violation",
                status = 409,
                detail = ex.Message,
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Path}", context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var payload = new
            {
                title = "Internal server error",
                status = 500,
                detail = "An unexpected error occurred.",
                traceId = context.TraceIdentifier
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
        }
    }
}
