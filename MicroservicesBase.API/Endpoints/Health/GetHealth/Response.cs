namespace MicroservicesBase.API.Endpoints.Health.GetHealth;

public sealed record Response
{
    public string Status { get; init; } = string.Empty;
    public double TotalDuration { get; init; }
    public Dictionary<string, HealthCheckEntry> Entries { get; init; } = new();
}

public sealed record HealthCheckEntry
{
    public string Status { get; init; } = string.Empty;
    public double Duration { get; init; }
    public string Description { get; init; } = string.Empty;
    public List<string> Tags { get; init; } = new();
}

