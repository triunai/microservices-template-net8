using Rgt.Space.Core.Domain.Contracts.Dashboard;

namespace Rgt.Space.Core.Abstractions.Dashboard;

public interface IDashboardReadDac
{
    Task<DashboardStatsResponse> GetStatsAsync(CancellationToken ct);
}
