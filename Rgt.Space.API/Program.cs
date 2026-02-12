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
    Log.Information("Starting up MicroservicesBase.API");

    var builder = WebApplication.CreateBuilder(args);

    // Only show PII in OIDC logs during development (tokens, emails, claims)
    Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = builder.Environment.IsDevelopment();

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

    // Add CORS (environment-conditional)
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("Default", policy =>
        {
            if (builder.Environment.IsDevelopment())
            {
                policy.AllowAnyOrigin()
                      .AllowAnyHeader()
                      .AllowAnyMethod();
            }
            else
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? throw new InvalidOperationException("Cors:AllowedOrigins must be configured for non-development environments.");
                policy.WithOrigins(allowedOrigins)
                      .WithHeaders("Authorization", "Content-Type", "X-Tenant", "X-Correlation-Id")
                      .WithMethods("GET", "POST", "PUT", "DELETE", "OPTIONS")
                      .AllowCredentials();
            }
        });
    });

    // Add Authentication with DUAL JWT Bearer support:
    // 1. SSO Broker tokens (RSA-SHA256 via OIDC Discovery)
    // 2. Local Login tokens (HMAC-SHA256 symmetric)
    builder.Services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = "MultiScheme";
            options.DefaultChallengeScheme = "MultiScheme";
        })
        .AddJwtBearer("SsoBearer", options =>
        {
            var authConfig = builder.Configuration.GetSection("Auth");

            // Disable claim type mapping to keep claims as-is
            options.MapInboundClaims = false;

            // Point to SSO Broker for OIDC Discovery
            options.Authority = authConfig["Authority"];
            options.Audience = authConfig["Audience"];

            // Enable HTTPS metadata discovery (set to false ONLY for localhost dev)
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

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

            // DEV ONLY: Hardcode RSA key to bypass Discovery issues
            if (builder.Environment.IsDevelopment())
            {
                try 
                {
                    var rsa = System.Security.Cryptography.RSA.Create();
                    rsa.ImportParameters(new System.Security.Cryptography.RSAParameters
                    {
                        Modulus = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes("rNH-ckvzkKRcqAKmb8CDdABZ-4_fUgI-vjSRoDfz-kCDtFdxTD69XvqUGP4NRyPXiSwI3ODh1_iBv-eg1RCBB8iA8eNLHuD5VbeMq4J5_ktCUjAUBQ783cs9R_7RKyLRrlW-Cq0EiZ-Z0I5vyWE9yzCN7Mf1MU2cn4GnAxMsJFlMwNEstbupqZWIgZXqLxrHcXcUpS-zpPkJULI4tDsUTjXMih8hU2ikrb_EltNYi0tcIBV6TfoBEc3OGiz8ao4mZ8UiKLBMwUi00qvQRtGl3xm0idh3sF2sGunIkTlRFtsBzjNpTqcAotyRXTNuQOTExX_dRL8C74eHUwd2J9quQQ"),
                        Exponent = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes("AQAB")
                    });

                    var key = new Microsoft.IdentityModel.Tokens.RsaSecurityKey(rsa) { KeyId = "rsa-2025-11-27" };

                    options.TokenValidationParameters.IssuerSigningKey = key;
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        Issuer = authConfig["Authority"],
                    };
                    options.Configuration.SigningKeys.Add(key);
                    
                    Log.Warning("DEV MODE: SsoBearer using Hardcoded RSA Key.");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to configure SSO hardcoded key.");
                }
            }

            // JIT User Provisioning for SSO tokens
            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                    var subject = context.Principal?.FindFirst("sub")?.Value
                                  ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var email = context.Principal?.FindFirst("email")?.Value
                                ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
                    var name = context.Principal?.FindFirst("name")?.Value
                               ?? context.Principal?.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? email;
                    var issuer = context.Principal?.FindFirst("iss")?.Value;

                    logger.LogDebug("SSO Token Validated. Subject: {Subject}", subject);

                    if (string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(email))
                    {
                        logger.LogError("SSO Token missing required claims. Sub: {Sub}, Email: {Email}", subject, email);
                        context.Fail("Token is missing required claims (sub, email)");
                        return;
                    }

                    var provider = issuer?.Contains("localhost") == true ? "sso_broker" : "azuread";

                    try
                    {
                        var syncService = context.HttpContext.RequestServices
                            .GetRequiredService<Rgt.Space.Core.Abstractions.Identity.IIdentitySyncService>();
                        var localUserId = await syncService.SyncOrGetUserAsync(provider, subject, email, name!, context.HttpContext.RequestAborted);
                        
                        var claimsIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                        claimsIdentity?.AddClaim(new System.Security.Claims.Claim("x-local-user-id", localUserId.ToString()));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Failed to sync user from SSO. Subject: {Subject}, Email: {Email}", subject, email);
                    }
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogDebug("SSO Authentication failed: {Exception}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        })
        .AddJwtBearer("LocalBearer", options =>
        {
            var localAuthConfig = builder.Configuration.GetSection("LocalAuth");

            // Disable claim type mapping
            options.MapInboundClaims = false;

            // Local token validation parameters (HMAC-SHA256)
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = localAuthConfig["Issuer"] ?? "rgt-space-portal",
                
                ValidateAudience = true,
                ValidAudience = localAuthConfig["Audience"] ?? "rgt-space-portal-api",
                
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(5),
                
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                    System.Text.Encoding.UTF8.GetBytes(
                        localAuthConfig["SigningKey"]
                            ?? throw new InvalidOperationException("LocalAuth:SigningKey must be configured. Use dotnet user-secrets for development."))),
            };

            options.Events = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerEvents
            {
                OnTokenValidated = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();

                    // For local tokens, the "sub" claim IS the local user ID
                    var userId = context.Principal?.FindFirst("sub")?.Value;
                    logger.LogDebug("Local Token Validated. Subject: {Subject}", userId);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        var claimsIdentity = context.Principal?.Identity as System.Security.Claims.ClaimsIdentity;
                        claimsIdentity?.AddClaim(new System.Security.Claims.Claim("x-local-user-id", userId));
                    }
                    
                    return Task.CompletedTask;
                },
                OnAuthenticationFailed = context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogDebug("Local Authentication failed: {Exception}", context.Exception.Message);
                    return Task.CompletedTask;
                }
            };
        })
        .AddPolicyScheme("MultiScheme", "SSO or Local", options =>
        {
            // Smart scheme selection: Try SSO first, then Local
            options.ForwardDefaultSelector = context =>
            {
                // Check if there's a Bearer token
                var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
                if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return "LocalBearer"; // Default to local if no token
                }

                // Extract the token
                var token = authHeader.Substring("Bearer ".Length).Trim();
                
                try
                {
                    // Peek at the token header to determine type
                    var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                    if (handler.CanReadToken(token))
                    {
                        var jwt = handler.ReadJwtToken(token);
                        
                        // If token has "kid" header, it's from SSO (RSA)
                        // If issuer is "rgt-space-portal", it's local
                        var issuer = jwt.Issuer;
                        if (issuer == "rgt-space-portal")
                        {
                            return "LocalBearer";
                        }
                    }
                }
                catch
                {
                    // If we can't parse it, try SSO first
                }

                return "SsoBearer";
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

        // Global rate limiter (IP-based partitioning — runs before auth/tenant resolution)
        options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
        {
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // Per-IP partition
            return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: clientIp,
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
                clientIp = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
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

    // 1. Global exception handler (MUST be first to catch all exceptions)
    app.UseExceptionHandler();

    // 2. Correlation ID middleware (generates/extracts X-Correlation-Id)
    app.UseMiddleware<CorrelationIdMiddleware>();

    // 3. Combo-Break Debugger headers (dev-only, X-Checkpoint-* on 5xx)
    app.UseComboBreakHeaders();

    // 4. Rate limiting (IP-based partitioning)
    app.UseRateLimiter();

    // 5. Rate limit info headers
    app.UseMiddleware<RateLimitHeadersMiddleware>();

    // 6. CORS (must be before Auth)
    app.UseCors("Default");

    // 7. Serilog request logging (enriched with correlation ID, tenant, client IP)
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

    // 8. Authentication (validates JWT tokens)
    app.UseAuthentication();

    // 9. Tenant resolution (AFTER auth — JWT tid claim now works)
    app.UseMiddleware<TenantResolutionMiddleware>();

    // 10. Load Permissions from DB (after AuthN + tenant, before AuthZ)
    app.UseMiddleware<PermissionLoadingMiddleware>();
    
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

// Required for integration tests (WebApplicationFactory<Program>)
public partial class Program { }
