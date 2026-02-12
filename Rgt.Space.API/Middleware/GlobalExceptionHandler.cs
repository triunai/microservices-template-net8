using Microsoft.AspNetCore.Diagnostics;
using Rgt.Space.Core.Abstractions.Debugging;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.API.Middleware
{
    /// <summary>
    /// Global exception handler using .NET 8's IExceptionHandler interface.
    /// Catches all unhandled exceptions and converts them to RFC 7807 ProblemDetails responses.
    /// Logs exceptions with Serilog including correlation ID, tenant context, and checkpoint info.
    /// </summary>
    public sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IHostEnvironment _environment;

        public GlobalExceptionHandler(
            ILogger<GlobalExceptionHandler> logger, 
            IHostEnvironment environment)
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
                // Resolve ICheckpointTracker from request services (scoped service)
                // Note: IExceptionHandler is singleton, ICheckpointTracker is scoped
                var checkpointTracker = httpContext.RequestServices.GetService<ICheckpointTracker>();
                
                // Capture checkpoint state BEFORE logging (no coalescing - preserve null signals)
                var checkpointCurrent = checkpointTracker?.Current;
                var checkpointLast = checkpointTracker?.Last;
                
                // Store in HttpContext.Items for ProblemDetailsFactory and middleware
                httpContext.Items[HttpConstants.ContextKeys.CheckpointCurrent] = checkpointCurrent;
                httpContext.Items[HttpConstants.ContextKeys.CheckpointLast] = checkpointLast;
                
                // Log the exception with full context
                LogException(httpContext, exception, checkpointCurrent, checkpointLast);

                // Create ProblemDetails response
                var includeDetails = _environment.IsDevelopment();
                problemDetails = API.ProblemDetails.ProblemDetailsFactory.CreateFromException(
                    httpContext,
                    exception,
                    includeDetails);
                
                // Phase 2: Record combo break (if error is recordable)
                RecordComboBreak(httpContext, exception, problemDetails, checkpointCurrent, checkpointLast);
            }

            // Set response status code
            httpContext.Response.StatusCode = problemDetails.Status ?? HttpConstants.StatusCodes.InternalServerError;

            // Write ProblemDetails as JSON response
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

            // Return true to indicate the exception was handled
            return true;
        }

        private void LogException(
            HttpContext httpContext, 
            Exception exception, 
            string? checkpointCurrent,
            string? checkpointLast)
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
                    // Still log COMBO BREAK for business errors — useful for debugging
                    _logger.LogWarning(
                        "💥 COMBO BREAK | CorrelationId: {CorrelationId} | Current: {CheckpointCurrent} | Last: {CheckpointLast} | Path: {Path}",
                        correlationId, checkpointCurrent ?? "(null)", checkpointLast ?? "(null)", httpContext.Request.Path);
                    
                    _logger.LogWarning(exception,
                        "Application error: {ErrorCode} | CorrelationId: {CorrelationId} | TenantId: {TenantId} | UserId: {UserId} | Path: {Path}",
                        appEx.ErrorCode, correlationId, tenantId, userId, httpContext.Request.Path);
                    break;

                case OperationCanceledException:
                    // Request was cancelled by client — NOT a combo break, don't log as such
                    _logger.LogInformation(
                        "Request cancelled: CorrelationId: {CorrelationId} | TenantId: {TenantId} | Path: {Path}",
                        correlationId, tenantId, httpContext.Request.Path);
                    break;

                default:
                    // Unexpected exceptions are errors — log COMBO BREAK for debugging
                    _logger.LogWarning(
                        "💥 COMBO BREAK | CorrelationId: {CorrelationId} | Current: {CheckpointCurrent} | Last: {CheckpointLast} | Path: {Path}",
                        correlationId, checkpointCurrent ?? "(null)", checkpointLast ?? "(null)", httpContext.Request.Path);
                    
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
        
        /// <summary>
        /// Records a combo break snapshot if the error is recordable.
        /// Uses ErrorCatalog.IsRecordableError() to filter routine errors.
        /// </summary>
        private void RecordComboBreak(
            HttpContext httpContext,
            Exception exception,
            Microsoft.AspNetCore.Mvc.ProblemDetails problemDetails,
            string? checkpointCurrent,
            string? checkpointLast)
        {
            try
            {
                // Get error code from exception or ProblemDetails
                var errorCode = exception is AppException appEx
                    ? appEx.ErrorCode
                    : problemDetails.Extensions.TryGetValue("errorCode", out var ec) 
                        ? ec?.ToString() ?? ErrorCatalog.INTERNAL_ERROR
                        : ErrorCatalog.INTERNAL_ERROR;
                
                // Check if this error should be recorded
                if (!ErrorCatalog.IsRecordableError(errorCode))
                {
                    return;
                }
                
                // Get recorder (may be NullComboBreakRecorder in production)
                var recorder = httpContext.RequestServices.GetService<IComboBreakRecorder>();
                if (recorder == null) return;
                
                // Extract route template (low-cardinality) instead of raw path
                var endpoint = httpContext.GetEndpoint() as Microsoft.AspNetCore.Routing.RouteEndpoint;
                var route = endpoint?.RoutePattern?.RawText
                            ?? httpContext.Request.Path.Value
                            ?? "unknown";
                
                // Get handler name (stored by CheckpointPipelineBehavior)
                var handlerName = httpContext.Items[HttpConstants.ContextKeys.CurrentHandlerName]?.ToString() ?? "unknown";
                
                // Get context values
                var correlationId = httpContext.Items[HttpConstants.ContextKeys.CorrelationId]?.ToString() ?? "unknown";
                var tenantId = httpContext.Items[HttpConstants.ContextKeys.TenantId]?.ToString();
                
                // Extract user ID safely (GUID only, no PII)
                Guid? userId = null;
                var userIdClaim = httpContext.User?.FindFirst("x-local-user-id")?.Value;
                if (Guid.TryParse(userIdClaim, out var parsedUserId))
                {
                    userId = parsedUserId;
                }
                
                // Phase 3: Calculate combo position (if ComboMeter is enabled)
                Core.Debugging.ComboPosition? comboPosition = null;
                var comboMapProvider = httpContext.RequestServices.GetService<IComboMapProvider>();
                if (comboMapProvider != null)
                {
                    comboPosition = Core.Debugging.ComboPositionCalculator.Calculate(
                        comboMapProvider,
                        handlerName,
                        checkpointCurrent,
                        checkpointLast);
                }
                
                // Create and record snapshot
                var snapshot = new Core.Debugging.ComboBreakSnapshot
                {
                    CorrelationId = correlationId,
                    Timestamp = DateTimeOffset.UtcNow,
                    CheckpointCurrent = checkpointCurrent,
                    CheckpointLast = checkpointLast,
                    TraceId = System.Diagnostics.Activity.Current?.TraceId.ToString(),
                    SpanId = System.Diagnostics.Activity.Current?.SpanId.ToString(),
                    Route = route,
                    Handler = handlerName,
                    ErrorCode = errorCode,
                    ErrorMessage = exception.Message,
                    TenantId = tenantId,
                    UserId = userId,
                    ComboPosition = comboPosition
                };
                
                recorder.Record(snapshot);
            }
            catch
            {
                // Fail-silent: Recording should never break the exception handler
            }
        }
    }
}

