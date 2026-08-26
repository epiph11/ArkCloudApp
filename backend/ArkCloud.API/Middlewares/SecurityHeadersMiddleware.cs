namespace ArkCloud.API.Middlewares;

/// <summary>Adds baseline security headers to every response. API is JSON-only, so the CSP is intentionally strict.</summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task Invoke(HttpContext context)
    {
        var headers = context.Response.Headers;

        headers["X-Content-Type-Options"] = "nosniff";
        headers["X-Frame-Options"] = "DENY";
        headers["Referrer-Policy"] = "no-referrer";
        headers["Permissions-Policy"] = "geolocation=(), microphone=(), camera=()";

        // The API's own JSON endpoints load nothing and embed nothing, so they get the
        // strictest possible CSP. Swagger UI (dev-only) is an actual HTML/JS/CSS app served
        // from the same host, and needs to load its own inline scripts/styles and call
        // /swagger/v1/swagger.json — "default-src 'none'" would silently break all of that.
        // 'unsafe-eval' is also allowed here (Swagger UI only): its bundled JSON-schema
        // validator (ajv) compiles validators via `new Function()` for "Try it out" on
        // complex request bodies — without it, Swagger UI loads but that feature silently
        // fails with a CSP violation. Scoped to /swagger only; every other response keeps
        // the fully strict policy below.
        headers["Content-Security-Policy"] = context.Request.Path.StartsWithSegments("/swagger")
            ? "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'"
            : "default-src 'none'; frame-ancestors 'none'";

        await _next(context);
    }
}
