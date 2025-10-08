using FastEndpoints;
using MicroservicesBase.API.Middleware;
using MicroservicesBase.Infrastructure;
using Microsoft.AspNetCore.Builder;
using FastEndpoints.Swagger;
using Serilog;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Infrastructure.Tenancy;
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

    // Add Serilog to the logging pipeline
    builder.Host.UseSerilog();

    // Configure ProblemDetails (RFC 7807 error responses)
    builder.Services.AddProblemDetails(options =>
    {
        options.CustomizeProblemDetails = ctx =>
        {
            // Add correlation ID to all ProblemDetails responses
            if (ctx.HttpContext.Items.TryGetValue("CorrelationId", out var correlationId))
            {
                ctx.ProblemDetails.Extensions["correlationId"] = correlationId?.ToString();
            }
            
            // Add tenant ID to all ProblemDetails responses
            if (ctx.HttpContext.Items.TryGetValue("TenantId", out var tenantId))
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
        .AddSqlServer(
            connectionString: builder.Configuration.GetConnectionString("TenantMaster")!,
            name: "master_database",
            tags: new[] { "db", "master", "ready" })
        .AddRedis(
            redisConnectionString: builder.Configuration.GetConnectionString("Redis")!,
            name: "redis_cache",
            tags: new[] { "cache", "redis", "ready" });

    // Add HttpContextAccessor (required for audit logging)
    builder.Services.AddHttpContextAccessor();

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
            config.PermitLimit = 100; // 100 requests
            config.Window = TimeSpan.FromSeconds(10); // per 10 seconds
            config.SegmentsPerWindow = 2; // Sliding window with 2 segments (5s each)
            config.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
            config.QueueLimit = 10; // Queue up to 10 requests when limit exceeded
        });

        // Global rate limiter (fallback for requests without tenant)
        options.GlobalLimiter = System.Threading.RateLimiting.PartitionedRateLimiter.Create<Microsoft.AspNetCore.Http.HttpContext, string>(httpContext =>
        {
            var tenantId = httpContext.Items["TenantId"]?.ToString() ?? "Unknown";
            
            // Per-tenant partition
            return System.Threading.RateLimiting.RateLimitPartition.GetSlidingWindowLimiter(
                partitionKey: tenantId,
                factory: _ => new System.Threading.RateLimiting.SlidingWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromSeconds(10),
                    SegmentsPerWindow = 2,
                    QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst,
                    QueueLimit = 10
                });
        });

        // Customize 429 response
        options.RejectionStatusCode = 429;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = 429;
            context.HttpContext.Response.Headers["Retry-After"] = "10";
            context.HttpContext.Response.Headers["X-RateLimit-Limit"] = "100";
            context.HttpContext.Response.Headers["X-RateLimit-Window"] = "10s";
            
            await context.HttpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://httpstatuses.com/429",
                title = "Too Many Requests",
                status = 429,
                detail = "Rate limit exceeded. Please retry after 10 seconds.",
                tenantId = context.HttpContext.Items["TenantId"]?.ToString() ?? "Unknown",
                correlationId = context.HttpContext.Items["CorrelationId"]?.ToString(),
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
            diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]?.ToString() ?? "Unknown");
            diagnosticContext.Set("TenantId", httpContext.Items["TenantId"]?.ToString() ?? "Unknown");
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
        };
    });
    
    // 6. FastEndpoints mapping (includes built-in exception handling)
    app.UseFastEndpoints();
    app.UseSwaggerGen();

    // 7. Health check endpoints
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
