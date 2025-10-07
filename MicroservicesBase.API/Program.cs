using FastEndpoints;
using MicroservicesBase.API.Middleware;
using MicroservicesBase.Infrastructure;
using Microsoft.AspNetCore.Builder;
using FastEndpoints.Swagger;
using Serilog;
using MicroservicesBase.Core.Abstractions.Tenancy;
using MicroservicesBase.Infrastructure.Tenancy;

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

    // Add infra services
    builder.Services.AddFastEndpoints();
    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.SwaggerDocument(); // Simple FastEndpoints Swagger - no custom config

    var app = builder.Build();

    // Middleware pipeline (ORDER MATTERS!)
    
    // 1. Correlation ID middleware (generates correlation ID for the request)
    app.UseMiddleware<CorrelationIdMiddleware>();
    
    // 2. Tenant resolution middleware (extracts tenant and enriches logs)
    app.UseMiddleware<TenantResolutionMiddleware>();
    
    // 3. Serilog request logging (logs HTTP requests with correlation ID)
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"]?.ToString() ?? "Unknown");
            diagnosticContext.Set("TenantId", httpContext.Items["TenantId"]?.ToString() ?? "Unknown");
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown");
        };
    });
    
    // 4. FastEndpoints mapping (includes built-in exception handling)
    app.UseFastEndpoints();
    app.UseSwaggerGen();

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
