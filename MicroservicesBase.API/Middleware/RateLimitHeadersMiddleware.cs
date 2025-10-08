namespace MicroservicesBase.API.Middleware;

/// <summary>
/// Middleware that adds rate limit information headers to all responses
/// </summary>
public class RateLimitHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public RateLimitHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add rate limit headers BEFORE calling next middleware (before response starts)
        context.Response.OnStarting(() =>
        {
            // Add rate limit headers if not already present (from rate limiter)
            if (!context.Response.Headers.ContainsKey("X-RateLimit-Limit"))
            {
                context.Response.Headers["X-RateLimit-Limit"] = "100";
                context.Response.Headers["X-RateLimit-Window"] = "10s";
                context.Response.Headers["X-RateLimit-Policy"] = "per-tenant-sliding-window";
            }
            return Task.CompletedTask;
        });

        // Call next middleware
        await _next(context);
    }
}

