namespace Rgt.Space.Core.Domain.Contracts.Dashboard;

public sealed record DashboardStatsResponse
{
    public DashboardKpis Kpis { get; init; } = new();
    public List<AssignmentDistribution> AssignmentDistribution { get; init; } = new();
    public List<VacantPosition> TopVacancies { get; init; } = new();
}

public sealed record DashboardKpis
{
    public int ActiveAssignments { get; init; }
    public int PendingVacancies { get; init; }
    public int ActiveProjects { get; init; }
    public int InactiveProjects { get; init; }
}

public sealed record AssignmentDistribution
{
    public string PositionCode { get; init; } = string.Empty;
    public int Count { get; init; }

    public AssignmentDistribution() { }

    public AssignmentDistribution(string positionCode, int count)
    {
        PositionCode = positionCode;
        Count = count;
    }
}

public sealed record VacantPosition
{
    public string ProjectName { get; init; } = string.Empty;
    public string MissingPosition { get; init; } = string.Empty;

    public VacantPosition() { }

    public VacantPosition(string projectName, string missingPosition)
    {
        ProjectName = projectName;
        MissingPosition = missingPosition;
    }
}
