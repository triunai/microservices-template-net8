using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Constants;
using Rgt.Space.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace Rgt.Space.API.Middleware
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
        private const string UnknownTenant = "Unknown";

        public TenantResolutionMiddleware(RequestDelegate next, ILogger<TenantResolutionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context, ITenantProvider tenantProvider)
        {
            var tenantId = context.Request.Headers[HttpConstants.Headers.Tenant].FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(tenantId))
            {
                // Set tenant in provider (for database access)
                if (tenantProvider is HeaderTenantProvider concrete)
                {
                    concrete.SetTenant(tenantId);
                }

                // Store in HttpContext.Items for access by other middleware/endpoints
                context.Items[HttpConstants.ContextKeys.TenantId] = tenantId;

                _logger.LogInformation("Tenant resolved: {TenantId}", tenantId);
            }
            else
            {
                // No tenant header provided - log warning and continue
                context.Items[HttpConstants.ContextKeys.TenantId] = UnknownTenant;
                _logger.LogWarning("No {HeaderName} header provided in request to {Path}", 
                    HttpConstants.Headers.Tenant, context.Request.Path);
            }

            // Push to Serilog's LogContext and continue pipeline
            using (LogContext.PushProperty(HttpConstants.ContextKeys.TenantId, 
                context.Items[HttpConstants.ContextKeys.TenantId]?.ToString() ?? UnknownTenant))
            {
                await _next(context);
            }
        }
    }
}
