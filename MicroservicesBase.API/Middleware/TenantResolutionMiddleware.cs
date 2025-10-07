using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace MicroservicesBase.API.Middleware
{
    /// <summary>
    /// Middleware that resolves the tenant context for the current request.
    /// Extracts tenant from X-Tenant header and:
    /// 1. Sets it in the ITenantProvider (for database connection resolution)
    /// 2. Stores it in HttpContext.Items (for access by other middleware)
    /// 3. Pushes it to Serilog's LogContext (for log enrichment)
    /// </summary>
    public class TenantResolutionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TenantResolutionMiddleware> _logger;

        public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
        {
            var tenantId = context.Request.Headers["X-Tenant"].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                // Set tenant in provider (for database access)
                if (tenantProvider is HeaderTenantProvider concrete)
                {
                    concrete.SetTenant(tenantId);
                }

                // Store in HttpContext.Items for access by other middleware/endpoints
                context.Items["TenantId"] = tenantId;

                _logger.LogInformation("Tenant resolved: {TenantId}", tenantId);
            }
            else
            {
                // No tenant header provided - log warning and continue
                context.Items["TenantId"] = "Unknown";
                _logger.LogWarning("No X-Tenant header provided in request to {Path}", context.Request.Path);
            }

            // Push to Serilog's LogContext and continue pipeline
            using (LogContext.PushProperty("TenantId", context.Items["TenantId"]?.ToString() ?? "Unknown"))
            {
                await _next(context);
            }
        }
    }
}
