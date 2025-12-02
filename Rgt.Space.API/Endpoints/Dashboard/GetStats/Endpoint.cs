using FastEndpoints;
using Rgt.Space.Core.Abstractions.Dashboard;
using Rgt.Space.Core.Domain.Contracts.Dashboard;

namespace Rgt.Space.API.Endpoints.Dashboard.GetStats;

public sealed class Endpoint : EndpointWithoutRequest<DashboardStatsResponse>
{
    private readonly IDashboardReadDac _dac;

    public Endpoint(IDashboardReadDac dac)
    {
        _dac = dac;
    }

    public override void Configure()
    {
        Get("/api/v1/dashboard/stats");
        // AllowAnonymous(); // TODO: Remove in Phase 2 (Auth is now live)
        
        Summary(s =>
        {
            s.Summary = "Get dashboard statistics";
            s.Description = "Retrieves aggregated KPIs, assignment distribution, and top vacancies for the dashboard.";
            s.Response<DashboardStatsResponse>(200, "Dashboard statistics retrieved successfully");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var stats = await _dac.GetStatsAsync(ct);
        await Send.OkAsync(stats, ct);
    }
}
