using Microsoft.AspNetCore.Mvc;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.API.Middleware
{
    /// <summary>
    /// Middleware to intercept 404 responses and return consistent ProblemDetails.
    /// Tells bad actors to stop hitting non-existent endpoints.
    /// </summary>
    public sealed class NotFoundMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<NotFoundMiddleware> _logger;

        public NotFoundMiddleware(RequestDelegate next, ILogger<NotFoundMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            // Intercept 404 responses and convert to 400 Bad Request ProblemDetails
            if (context.Response.StatusCode == HttpConstants.StatusCodes.NotFound && !context.Response.HasStarted)
            {
                // Don't log 404s - they're noise from bad actors and crawlers
                var problemDetails = CreateBadRequestProblemDetails(context);

                // Clear any existing response content
                context.Response.Clear();
                context.Response.StatusCode = HttpConstants.StatusCodes.BadRequest;
                context.Response.ContentType = Core.Constants.File.Mime.APP_PROBLEM_JSON;

                await context.Response.WriteAsJsonAsync(problemDetails);
            }
        }

        /// <summary>
        /// Creates a ProblemDetails response for invalid requests.
        /// Tells bad actors to stop making malformed requests.
        /// </summary>
        private static Microsoft.AspNetCore.Mvc.ProblemDetails CreateBadRequestProblemDetails(HttpContext context)
        {
            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = HttpConstants.ProblemTypes.BadRequest,
                Title = "Invalid request",
                Status = HttpConstants.StatusCodes.BadRequest,
                Detail = "The request is malformed or invalid. Check your endpoint URL and parameters. Do not retry with invalid requests.",
                Instance = context.Request.Path
            };

            // Add context enrichment (same as GlobalExceptionHandler)
            if (context.Items.TryGetValue(HttpConstants.ContextKeys.CorrelationId, out var correlationId))
            {
                problemDetails.Extensions["correlationId"] = correlationId?.ToString();
            }

            if (context.Items.TryGetValue(HttpConstants.ContextKeys.TenantId, out var tenantId))
            {
                problemDetails.Extensions["tenantId"] = tenantId?.ToString();
            }

            problemDetails.Extensions["traceId"] = context.TraceIdentifier;
            problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;
            
            // Add a hint about valid endpoints (without being too helpful to attackers)
            problemDetails.Extensions["hint"] = "Verify request format and endpoint structure";

            return problemDetails;
        }
    }
}
