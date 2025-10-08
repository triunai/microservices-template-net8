using FastEndpoints;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MicroservicesBase.API.Endpoints.Health.GetLiveness;

/// <summary>
/// Liveness health check endpoint (Swagger-visible version).
/// Returns healthy if the API process is running. Does not check dependencies.
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
        Get("/health/live");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Liveness probe - is the API process running?";
            s.Description = "Returns healthy if the process is alive. Does not check any dependencies. Used by Kubernetes liveness probes.";
            s.Response<Response>(200, "API is alive");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var startTime = DateTimeOffset.UtcNow;

        // Run health checks with no predicate (will return healthy since liveness checks nothing)
        var report = await _healthCheckService.CheckHealthAsync(_ => false, ct);

        var duration = DateTimeOffset.UtcNow - startTime;

        await Send.OkAsync(new Response
        {
            Status = report.Status.ToString(),
            TotalDuration = duration.TotalMilliseconds,
            Message = "API process is running"
        }, ct);
    }
}

