namespace EquipFlow.WebApi.Middleware;

/// <summary>
/// Injects standard OWASP security headers into all HTTP responses to mitigate 
/// common web vulnerabilities (GAP-7 / SEC-011).
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent MIME type sniffing
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        
        // Prevent clickjacking
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        
        // Disable legacy XSS auditors (modern browsers use CSP)
        context.Response.Headers.Append("X-XSS-Protection", "0");
        
        // Control referrer information leakage
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        
        // Restrict browser features
        context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
        
        // Restrict resource loading to same origin
        context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'");
        
        // Enforce HTTPS (HSTS)
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

        await _next(context);
    }
}
