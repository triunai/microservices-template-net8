using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Core.Constants;
using Rgt.Space.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Http;
using Serilog.Context;
using System.Security.Claims;

namespace Rgt.Space.API.Middleware
{
    /// <summary>
    /// Middleware that resolves the tenant context for the current request.
    /// Priority order for tenant resolution:
    /// 1. JWT "tid" claim (most secure - from authenticated token)
    /// 2. X-Tenant header (fallback for non-authenticated endpoints)
    /// 3. Query parameter "tenantId" (fallback for webhooks/callbacks)
    /// 
    /// Responsibilities:
    /// 1. Sets tenant in ITenantProvider (for database connection resolution)
    /// 2. Stores tenant in HttpContext.Items (for access by other middleware)
    /// 3. Pushes tenant to Serilog's LogContext (for log enrichment)
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
            string? tenantCode = null;
            string? source = null;

            // Priority 1: JWT "tid" claim (from SSO Broker or API-issued tokens)
            if (context.User?.Identity?.IsAuthenticated == true)
            {
                tenantCode = context.User.FindFirst("tid")?.Value;
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    source = "JWT-tid-claim";
                    _logger.LogDebug("Tenant resolved from JWT tid claim: {TenantCode}", tenantCode);
                }
            }

            // Priority 2: X-Tenant header (fallback for non-authenticated requests)
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                tenantCode = context.Request.Headers[HttpConstants.Headers.Tenant].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    source = "X-Tenant-header";
                    _logger.LogDebug("Tenant resolved from X-Tenant header: {TenantCode}", tenantCode);
                }
            }

            // Priority 3: Query parameter (fallback for webhooks/callbacks)
            if (string.IsNullOrWhiteSpace(tenantCode))
            {
                tenantCode = context.Request.Query["tenantId"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(tenantCode))
                {
                    source = "query-parameter";
                    _logger.LogDebug("Tenant resolved from query parameter: {TenantCode}", tenantCode);
                }
            }

            // Handle tenant resolution result
            if (!string.IsNullOrWhiteSpace(tenantCode))
            {
                // Set tenant in provider (for database access)
                if (tenantProvider is HeaderTenantProvider concrete)
                {
                    concrete.SetTenant(tenantCode);
                }

                // Store in HttpContext.Items for access by other middleware/endpoints
                context.Items[HttpConstants.ContextKeys.TenantId] = tenantCode;

                _logger.LogInformation("Tenant resolved: {TenantCode} (source: {Source})", tenantCode, source);
            }
            else
            {
                // No tenant found in any source
                context.Items[HttpConstants.ContextKeys.TenantId] = UnknownTenant;
                
                // Only log warning for authenticated requests (they should have tid claim)
                if (context.User?.Identity?.IsAuthenticated == true)
                {
                    _logger.LogWarning("Authenticated request to {Path} has no tenant context (no tid claim, X-Tenant header, or tenantId query param)", 
                        context.Request.Path);
                }
                else
                {
                    _logger.LogDebug("Unauthenticated request to {Path} has no tenant context", 
                        context.Request.Path);
                }
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
