using FastEndpoints;
using Rgt.Space.API.Middleware;
using Rgt.Space.Core.Constants;
using Rgt.Space.Infrastructure;
using Microsoft.AspNetCore.Builder;
using FastEndpoints.Swagger;
using Serilog;
using Rgt.Space.Core.Abstractions.Tenancy;
using Rgt.Space.Infrastructure.Tenancy;
using Microsoft.AspNetCore.RateLimiting;

// Configure Serilog early (before building the app)
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json")
        .Build())
    .Enrich.FromLogContext()
    .CreateLogger();

try
{
    // Enable PII logging to debug OIDC issues
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;

    Log.Information("Starting up MicroservicesBase.API");

    var builder = WebApplication.CreateBuilder(args);

    // Add Serilog to the logging pipeline
    builder.Host.UseSerilog();

    // Configure ProblemDetails (RFC 7807 error responses)
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = ctx =>
        {
            // Add correlation ID to all ProblemDetails responses
            if (ctx.HttpContext.Items.TryGetValue(HttpConstants.ContextKeys.CorrelationId, out var correlationId))
            {
                ctx.ProblemDetails.Extensions["correlationId"] = correlationId?.ToString();
            }
            
            // Add tenant ID to all ProblemDetails responses
            if (ctx.HttpContext.Items.TryGetValue(HttpConstants.ContextKeys.TenantId, out var tenantId))
            {
                ctx.ProblemDetails.Extensions["tenantId"] = tenantId?.ToString();
            }
            
            // Add trace ID
            ctx.ProblemDetails.Extensions["traceId"] = ctx.HttpContext.TraceIdentifier;
            
            // Add timestamp
            ctx.ProblemDetails.Extensions["timestamp"] = DateTimeOffset.UtcNow;
        };
    });

    // Add global exception handler (.NET 8 approach)
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddNpgSql(
            connectionString: builder.Configuration.GetConnectionString("PortalDb")!,
            name: "portal_database",
            tags: new[] { "db", "portal", "ready" })
        .AddRedis(
            redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
            name: "redis_cache",
            tags: new[] { "cache", "redis" }); // No "ready" tag - API works without Redis (conn strings use IMemoryCache)

    // Add HttpContextAccessor (required for audit logging)
    builder.Services.AddHttpContextAccessor();

    // Add Authentication with JWT Bearer (RSA-SHA256 via OIDC Discovery)
    builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            var authConfig = builder.Configuration.GetSection("Auth");

            // Disable claim type mapping to keep claims as-is (e.g. "sub" -> "sub")
            options.MapInboundClaims = false;

            // Point to SSO Broker for OIDC Discovery
            options.Authority = authConfig["Authority"];
            options.Audience = authConfig["Audience"];

            // Enable HTTPS metadata discovery (set to false ONLY for localhost dev with self-signed certs)
            options.RequireHttpsMetadata = false; // TODO: Set to true in production

            // Token validation parameters
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = authConfig["Authority"],

                ValidateAudience = true,
                ValidAudience = authConfig["Audience"],

                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
            };

            // DEV ONLY: DIAGNOSTIC - Hardcode the key to bypass Discovery issues
            if (builder.Environment.IsDevelopment())
            {
                try 
                {
                    var rsa = System.Security.Cryptography.RSA.Create();
                    rsa.ImportParameters(new System.Security.Cryptography.RSAParameters
                    {
                        // Values from https://localhost:7012/.well-known/jwks.json
                        Modulus = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes("rNH-ckvzkKRcqAKmb8CDdABZ-4_fUgI-vjSRoDfz-kCDtFdxTD69XvqUGP4NRyPXiSwI3ODh1_iBv-eg1RCBB8iA8eNLHuD5VbeMq4J5_ktCUjAUBQ783cs9R_7RKyLRrlW-Cq0EiZ-Z0I5vyWE9yzCN7Mf1MU2cn4GnAxMsJFlMwNEstbupqZWIgZXqLxrHcXcUpS-zpPkJULI4tDsUTjXMih8hU2ikrb_EltNYi0tcIBV6TfoBEc3OGiz8ao4mZ8UiKLBMwUi00qvQRtGl3xm0idh3sF2sGunIkTlRFtsBzjNpTqcAotyRXTNuQOTExX_dRL8C74eHUwd2J9quQQ"),
                        Exponent = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes("AQAB")
                    });

                    var key = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) { KeyId = "rsa-2025-11-27" };

                    // FORCE THE KEY and DISABLE DISCOVERY
                    options.TokenValidationParameters.IssuerSigningKey = key;
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        Issuer = authConfig["Authority"],
                    };
                    options.Configuration.SigningKeys.Add(key);
                    
                    Log.Warning("DEV MODE: Using Hardcoded RSA Key for Token Validation to bypass OIDC Discovery.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to configure hardcoded key.");
                }
            }
            else 
            {
                // Production or other envs: Use standard discovery
                // (The previous explicit config manager code was removed for this test, 
                // but in prod we would just rely on default behavior)
            }

            // JIT User Provisioning: Sync user from SSO on every token validation
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    // Extract claims from the validated token
                    var subject = context.Principal?.FindFirst("sub")?.Value;
                    var email = context.Principal?.FindFirst("email")?.Value;
                    var name = context.Principal?.FindFirst("name")?.Value ?? email;
                    var issuer = context.Principal?.FindFirst("iss")?.Value;

                    if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
                    {
                        context.Fail("Token is missing required claims (sub, email)");
                        return;
                    }

                    // Determine provider from issuer (e.g., "https://localhost:7012" -> "sso_broker")
                    var provider = issuer?.Contains("localhost") == true ? "sso_broker" : "azuread";

                    try
                    {
                        // Resolve Identity Sync Service and sync the user
                        var syncService = context.HttpContext.RequestServices
                            .GetRequiredService<Rgt.Space.Core.Abstractions.Identity.IIdentitySyncService>();

                        // Sync user and get Local ID
                        var localUserId = await syncService.SyncOrGetUserAsync(provider, subject, email, name!, context.HttpContext.RequestAborted);
                        
                        // Attach Local ID to Principal
                        var claimsIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                        claimsIdentity?.AddClaim(new System.Security.Claims.Claim("x-local-user-id", localUserId.ToString()));
                    }
                    catch (Exception ex)
                    {
                        // Log the error but don't fail authentication (user might already exist)
                        var logger = context.HttpContext.RequestServices
                            .GetRequiredService<ILogger<Program>>();
                        logger.LogError(ex, "Failed to sync user from SSO. Subject: {Subject}, Email: {Email}", subject, email);

                        // Don't fail auth - user might already be provisioned
                        // context.Fail() would reject valid tokens which is worse than missing a sync
                    }
                },

                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices
                        .GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("JWT Authentication failed: {Exception}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // Add API versioning
    builder.Services.AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true; // Add X-Api-Versions header to responses
        options.ApiVersionReader = Asp.Versioning.ApiVersionReader.Combine(
            new Asp.Versioning.UrlSegmentApiVersionReader(),
            new Asp.Versioning.HeaderApiVersionReader("X-Api-Version")
        );
    }).AddApiExplorer(options =>
    {
        options.GroupNameFormat = "'v'VVV"; // Format: v1, v2, etc.
        options.SubstituteApiVersionInUrl = true;
    });

    // Add rate limiting
    builder.Services.AddRateLimiter(options =>
    {
        // Per-tenant rate limiting using sliding window
        options.AddSlidingWindowLimiter("per-tenant", config =>
        {
            config.PermitLimit = 1000; // 1000 requests (increased for load testing)
            config.Window = TimeSpan.FromSeconds(10); // per 10 seconds
            config.SegmentsPerWindow = 2; // Sliding window with 2 segments (5s each)
            config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            config.QueueLimit = 10; // Queue up to 10 requests when limit exceeded
        });

        // Global rate limiter (fallback for requests without tenant)
        options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
        {
            var tenantId = httpContext.Items[HttpConstants.ContextKeys.TenantId]?.ToString() ?? "Unknown";
            
            // Per-tenant partition
            return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: tenantId,
                factory: _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromSeconds(10),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                });
        });

        // Customize 429 response
        options.RejectionStatusCode = HttpConstants.StatusCodes.TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = HttpConstants.StatusCodes.TooManyRequests;
            context.HttpContext.Response.Headers[HttpConstants.Headers.RetryAfter] = "10";
            context.HttpContext.Response.Headers[HttpConstants.Headers.RateLimitLimit] = "1000";
            context.HttpContext.Response.Headers[HttpConstants.Headers.RateLimitWindow] = "10s";
            
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                type = HttpConstants.ProblemTypes.TooManyRequests,
                title = "Too Many Requests",
                status = HttpConstants.StatusCodes.TooManyRequests,
                detail = "Rate limit exceeded. Please retry after 10 seconds.",
                tenantId = context.HttpContext.Items[HttpConstants.ContextKeys.TenantId]?.ToString() ?? "Unknown",
                correlationId = context.HttpContext.Items[HttpConstants.ContextKeys.CorrelationId]?.ToString(),
                timestamp = DateTimeOffset.UtcNow
            }, cancellationToken);
        };
    });

    // Add infra services
    builder.Services.AddFastEndpoints();
    builder.Services.AddInfrastructure(builder.Configuration);
    
    // Configure Swagger with versioning
    builder.Services.SwaggerDocument(o =>
    {
        o.DocumentSettings = s =>
        {
            s.DocumentName = "v1";
            s.Title = "Microservices Base API";
            s.Version = "v1.0";
            s.Description = "Multi-tenant POS Microservice Template - Version 1";
        };
        o.ShortSchemaNames = true;
    });

    var app = builder.Build();

    // Middleware pipeline (ORDER MATTERS!)
    
    // 1. Correlation ID middleware (generates correlation ID for the request)
    app.UseMiddleware<CorrelationIdMiddleware>();
    
    // 2. Tenant resolution middleware (extracts tenant and enriches logs)
    app.UseMiddleware<TenantResolutionMiddleware>();
    
    // 3. Rate limiting (after tenant resolution, so it can partition by tenant)
    app.UseRateLimiter();
    
    // 4. Rate limit info headers (add rate limit info to all responses)
    app.UseMiddleware<RateLimitHeadersMiddleware>();
    
    // 5. Serilog request logging (logs HTTP requests with correlation ID)
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            const string unknown = "Unknown";
            diagnosticContext.Set(HttpConstants.ContextKeys.CorrelationId, 
                httpContext.Items[HttpConstants.ContextKeys.CorrelationId]?.ToString() ?? unknown);
            diagnosticContext.Set(HttpConstants.ContextKeys.TenantId, 
                httpContext.Items[HttpConstants.ContextKeys.TenantId]?.ToString() ?? unknown);
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? unknown);
        };
    });
    
    // 6. Authentication & Authorization (validates JWT tokens and checks permissions)
    app.UseAuthentication();
    app.UseAuthorization();
    
    // 7. FastEndpoints mapping (includes built-in exception handling)
    app.UseFastEndpoints();
    app.UseSwaggerGen();

    // 8. 404 Not Found middleware (intercepts 404s and returns ProblemDetails)
    app.UseMiddleware<NotFoundMiddleware>();

    // 8. Health check endpoints
    // Liveness: Basic check - is the process running?
    app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = _ => false, // Don't run any registered health checks, just return healthy if process is alive
        ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
    });

    // Readiness: Deep check - is the API ready to serve requests? (Master DB + Redis)
    app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        Predicate = check => check.Tags.Contains("ready"), // Only run checks tagged with "ready"
        ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
    });

    // General health endpoint (runs all health checks)
    app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
    {
        ResponseWriter = HealthChecks.UI.Client.UIResponseWriter.WriteHealthCheckUIResponse
    });

    Log.Information("Application startup complete");  
    Log.Information("Listening on: {Urls}", string.Join(", ", builder.WebHost.GetSetting("urls")?.Split(';') ?? new[] { "default" }));

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
