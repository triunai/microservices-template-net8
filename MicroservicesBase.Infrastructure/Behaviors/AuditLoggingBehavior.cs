using MediatR;
using MicroservicesBase.Core.Abstractions.Auditing;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Core.Configuration;
using MicroservicesBase.Core.Domain.Auditing;
using MicroservicesBase.Infrastructure.Auditing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace MicroservicesBase.Infrastructure.Behaviors;

/// <summary>
/// MediatR pipeline behavior that automatically audits all queries and commands.
/// Captures: who, what, when, where, result, duration.
/// </summary>
public sealed class AuditLoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAuditLogger _auditLogger;
    private readonly ITenantProvider _tenantProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly AuditSettings _settings;
    private readonly ILogger<AuditLoggingBehavior<TRequest, TResponse>> _logger;

    public AuditLoggingBehavior(
        IAuditLogger auditLogger,
        ITenantProvider tenantProvider,
        IHttpContextAccessor httpContextAccessor,
        IOptions<AuditSettings> settings,
        ILogger<AuditLoggingBehavior<TRequest, TResponse>> logger)
    {
        _auditLogger = auditLogger;
        _tenantProvider = tenantProvider;
        _httpContextAccessor = httpContextAccessor;
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Skip if audit logging is disabled
        if (!_settings.Enabled)
            return await next();

        // Apply sampling for read operations
        if (IsReadOperation(typeof(TRequest).Name) && !ShouldSampleRead())
            return await next();

        var stopwatch = Stopwatch.StartNew();
        var httpContext = _httpContextAccessor.HttpContext;
        
        // Capture context before execution
        var auditEntry = new AuditEntry
        {
            TenantId = _tenantProvider.Id ?? "Unknown",
            UserId = httpContext?.User?.Identity?.Name, // Will be populated from JWT in future
            IpAddress = GetClientIpAddress(httpContext),
            UserAgent = httpContext?.Request.Headers["User-Agent"].ToString(),
            Action = DetermineAction(typeof(TRequest).Name),
            EntityType = ExtractEntityType(typeof(TRequest).Name),
            EntityId = ExtractEntityId(request),
            Timestamp = DateTimeOffset.UtcNow,
            CorrelationId = httpContext?.Items["CorrelationId"]?.ToString(),
            RequestPath = httpContext?.Request.Path.ToString(),
            Source = "API",
            IsSuccess = false, // Will be updated after execution
            RequestData = _settings.Payloads.LogRequests 
                ? PayloadProcessor.ProcessPayload(request, _settings.Payloads) 
                : null
        };

        TResponse? response = default;
        Exception? exception = null;

        try
        {
            // Execute the handler
            response = await next();

            // Capture success
            auditEntry = auditEntry with
            {
                IsSuccess = true,
                StatusCode = 200,
                DurationMs = (int)stopwatch.ElapsedMilliseconds,
                ResponseData = _settings.Payloads.LogResponses 
                    ? PayloadProcessor.ProcessPayload(response, _settings.Payloads) 
                    : null
            };
        }
        catch (Exception ex)
        {
            exception = ex;
            
            // Capture failure
            auditEntry = auditEntry with
            {
                IsSuccess = false,
                StatusCode = DetermineStatusCodeFromException(ex),
                ErrorCode = ExtractErrorCode(ex),
                ErrorMessage = ex.Message,
                DurationMs = (int)stopwatch.ElapsedMilliseconds
            };

            throw;
        }
        finally
        {
            stopwatch.Stop();

            // Log audit entry asynchronously (fire-and-forget via channel)
            try
            {
                await _auditLogger.LogAsync(auditEntry, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to enqueue audit entry for {Action}", auditEntry.Action);
            }
        }

        return response!;
    }

    /// <summary>
    /// Determine if this is a read operation based on request name
    /// </summary>
    private static bool IsReadOperation(string requestName)
    {
        return requestName.Contains("Query", StringComparison.OrdinalIgnoreCase) ||
               requestName.Contains("Get", StringComparison.OrdinalIgnoreCase) ||
               requestName.Contains("Read", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Apply sampling for read operations
    /// </summary>
    private bool ShouldSampleRead()
    {
        var random = Random.Shared.Next(1, 101); // 1-100
        return random <= _settings.Sampling.ReadsPercent;
    }

    /// <summary>
    /// Determine action taxonomy from request name (e.g., "GetSaleByIdQuery" → "Sales.Read")
    /// </summary>
    private static string DetermineAction(string requestName)
    {
        // Extract entity and verb from request name
        // Examples:
        // - GetSaleByIdQuery → Sales.Read
        // - CreateSaleCommand → Sales.Create
        // - VoidSaleCommand → Sales.Void

        var name = requestName.Replace("Query", "").Replace("Command", "");

        if (name.StartsWith("Get") || name.Contains("Read"))
            return $"{ExtractEntityType(name)}.Read";
        if (name.StartsWith("Create"))
            return $"{ExtractEntityType(name)}.Create";
        if (name.StartsWith("Update"))
            return $"{ExtractEntityType(name)}.Update";
        if (name.StartsWith("Delete"))
            return $"{ExtractEntityType(name)}.Delete";
        if (name.Contains("Void"))
            return $"{ExtractEntityType(name)}.Void";
        if (name.Contains("Refund"))
            return $"{ExtractEntityType(name)}.Refund";

        return name; // Fallback
    }

    /// <summary>
    /// Extract entity type from request name (e.g., "GetSaleByIdQuery" → "Sale")
    /// </summary>
    private static string? ExtractEntityType(string requestName)
    {
        // Simple heuristic: find first capital letter sequence
        var match = System.Text.RegularExpressions.Regex.Match(requestName, @"(?:Get|Create|Update|Delete|Void|Refund)([A-Z][a-z]+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extract entity ID from request (if it has an Id property)
    /// </summary>
    private static string? ExtractEntityId(TRequest request)
    {
        var idProperty = typeof(TRequest).GetProperty("Id") ?? 
                         typeof(TRequest).GetProperty("SaleId") ??
                         typeof(TRequest).GetProperty("EntityId");

        return idProperty?.GetValue(request)?.ToString();
    }

    /// <summary>
    /// Get client IP address (X-Forwarded-For aware)
    /// </summary>
    private static string? GetClientIpAddress(HttpContext? context)
    {
        if (context == null)
            return null;

        // Check X-Forwarded-For header first (for proxies/load balancers)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            var ips = forwardedFor.Split(',');
            return ips[0].Trim(); // First IP is the original client
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }

    /// <summary>
    /// Determine HTTP status code from exception
    /// </summary>
    private static int DetermineStatusCodeFromException(Exception exception)
    {
        return exception switch
        {
            InvalidOperationException => 404,
            ArgumentException => 400,
            UnauthorizedAccessException => 401,
            _ => 500
        };
    }

    /// <summary>
    /// Extract error code from exception (if it has one)
    /// </summary>
    private static string? ExtractErrorCode(Exception exception)
    {
        // Check if exception has an ErrorCode property (custom exceptions)
        var errorCodeProperty = exception.GetType().GetProperty("ErrorCode");
        return errorCodeProperty?.GetValue(exception)?.ToString();
    }
}

