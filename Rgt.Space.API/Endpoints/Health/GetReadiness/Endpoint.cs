using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Rgt.Space.API.Endpoints.Health.GetReadiness;

/// <summary>
/// Readiness health check endpoint (Swagger-visible version).
/// Returns healthy if Master DB and Redis are accessible and ready to serve traffic.
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
        Get("/health/ready");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Readiness probe - is the API ready to serve traffic?";
            s.Description = "Checks Master DB and Redis connectivity. Returns healthy only if all dependencies are accessible. Used by Kubernetes readiness probes and load balancers.";
            s.Response<Response>(200, "API is ready to serve traffic");
            s.Response<Response>(503, "API is not ready (dependencies unhealthy)");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Run health checks tagged with "ready"
        var report = await _healthCheckService.CheckHealthAsync(check => check.Tags.Contains("ready"), ct);

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

