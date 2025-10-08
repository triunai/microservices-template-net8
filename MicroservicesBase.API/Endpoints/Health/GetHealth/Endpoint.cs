using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroservicesBase.API.Endpoints.Health.GetHealth;

/// <summary>
/// General health check endpoint (Swagger-visible version).
/// Runs all registered health checks and returns detailed status.
/// </summary>
public sealed class Endpoint : EndpointWithoutRequest<Response>
{
    private readonly HealthCheckService _healthCheckService;

    public Endpoint(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    public override void Configure()
    {
        Get("/health");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "General health check - run all checks";
            s.Description = "Runs all registered health checks (Master DB, Redis, etc.) and returns detailed status for each. Used for monitoring dashboards and diagnostics.";
            s.Response<Response>(200, "All checks passed");
            s.Response<Response>(503, "One or more checks failed");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Run all health checks (no predicate filter)
        var report = await _healthCheckService.CheckHealthAsync(ct);

        var duration = DateTimeOffset.UtcNow - startTime;

        var entries = report.Entries.ToDictionary(
            entry => entry.Key,
            entry => new HealthCheckEntry
            {
                Status = entry.Value.Status.ToString(),
                Duration = entry.Value.Duration.TotalMilliseconds,
                Description = entry.Value.Description ?? string.Empty,
                Tags = entry.Value.Tags.ToList()
            });

        var response = new Response
        {
            Status = report.Status.ToString(),
            TotalDuration = duration.TotalMilliseconds,
            Entries = entries
        };

        // Return 503 if unhealthy, 200 if healthy
        var statusCode = report.Status == HealthStatus.Healthy ? 200 : 503;

        await Send.ResponseAsync(response, statusCode, ct);
    }
}

