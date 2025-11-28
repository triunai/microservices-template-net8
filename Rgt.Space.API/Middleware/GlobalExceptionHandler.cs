using Microsoft.AspNetCore.Diagnostics;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.API.Middleware
{
    /// <summary>
    /// Global exception handler using .NET 8's IExceptionHandler interface.
    /// Catches all unhandled exceptions and converts them to RFC 7807 ProblemDetails responses.
    /// Logs exceptions with Serilog including correlation ID and tenant context.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment environment)
        {
            _logger = logger;
            _environment = environment;
        }

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken)
        {
            Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails;

            // Handle routing constraint failures (e.g., invalid GUID format)
            if (IsRoutingConstraintFailure(exception))
            {
                // Don't log routing failures - they're noise from bad actors
                problemDetails = CreateRoutingConstraintError(httpContext, exception);
            }
            else
            {
                // Log the exception with full context
                LogException(httpContext, exception);

                // Create ProblemDetails response
                var includeDetails = _environment.IsDevelopment();
                problemDetails = API.ProblemDetails.ProblemDetailsFactory.CreateFromException(
                    httpContext,
                    exception,
                    includeDetails);
            }

            // Set response status code
            httpContext.Response.StatusCode = problemDetails.Status ?? HttpConstants.StatusCodes.InternalServerError;

            // Write ProblemDetails as JSON response
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            // Return true to indicate the exception was handled
            return true;
        }

        private void LogException(HttpContext httpContext, Exception exception)
        {
            const string unknown = "Unknown";
            const string anonymous = "Anonymous";
            
            var correlationId = httpContext.Items[HttpConstants.ContextKeys.CorrelationId]?.ToString() ?? unknown;
            var tenantId = httpContext.Items[HttpConstants.ContextKeys.TenantId]?.ToString() ?? unknown;
            var userId = httpContext.User?.Identity?.Name ?? anonymous;

            // Different log levels based on exception type
            switch (exception)
            {
                case AppException appEx:
                    // Application exceptions are expected business errors (log as warning)
                    _logger.LogWarning(exception,
                        "Application error: {ErrorCode} | CorrelationId: {CorrelationId} | TenantId: {TenantId} | UserId: {UserId} | Path: {Path}",
                        appEx.ErrorCode, correlationId, tenantId, userId, httpContext.Request.Path);
                    break;

                case OperationCanceledException:
                    // Request was cancelled by client (log as information)
                    _logger.LogInformation(
                        "Request cancelled: CorrelationId: {CorrelationId} | TenantId: {TenantId} | Path: {Path}",
                        correlationId, tenantId, httpContext.Request.Path);
                    break;

                default:
                    // Unexpected exceptions are errors (log as error)
                    _logger.LogError(exception,
                        "Unhandled exception: {ExceptionType} | CorrelationId: {CorrelationId} | TenantId: {TenantId} | UserId: {UserId} | Path: {Path}",
                        exception.GetType().Name, correlationId, tenantId, userId, httpContext.Request.Path);
                    break;
            }
        }

        /// <summary>
        /// Determines if the exception is from a routing constraint failure (e.g., invalid GUID).
        /// </summary>
        private static bool IsRoutingConstraintFailure(Exception exception)
        {
            return exception is BadHttpRequestException badRequest &&
                   (badRequest.Message.Contains("Could not bind parameter") ||
                    badRequest.Message.Contains("The value") && badRequest.Message.Contains("is not valid") ||
                    badRequest.Message.Contains("Failed to bind parameter"));
        }

        /// <summary>
        /// Creates a ProblemDetails response for routing constraint failures.
        /// Tells bad actors to stop hitting invalid endpoints.
        /// </summary>
        private Microsoft.AspNetCore.Mvc.ProblemDetails CreateRoutingConstraintError(
            HttpContext httpContext, 
            Exception exception)
        {
            var problemDetails = new Microsoft.AspNetCore.Mvc.ProblemDetails
            {
                Type = HttpConstants.ProblemTypes.BadRequest,
                Title = "Invalid request format",
                Status = HttpConstants.StatusCodes.BadRequest,
                Detail = "The request contains invalid parameter format. This endpoint only accepts valid GUIDs. Please check your request and do not retry with invalid formats.",
                Instance = httpContext.Request.Path
            };

            // Add context enrichment (correlation ID, tenant ID, etc.)
            if (httpContext.Items.TryGetValue(HttpConstants.ContextKeys.CorrelationId, out var correlationId))
            {
                problemDetails.Extensions["correlationId"] = correlationId?.ToString();
            }

            if (httpContext.Items.TryGetValue(HttpConstants.ContextKeys.TenantId, out var tenantId))
            {
                problemDetails.Extensions["tenantId"] = tenantId?.ToString();
            }

            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;
            problemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;
            
            // Add a hint for valid format (without being too helpful to attackers)
            problemDetails.Extensions["expectedFormat"] = "GUID (xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx)";

            return problemDetails;
        }
    }
}

