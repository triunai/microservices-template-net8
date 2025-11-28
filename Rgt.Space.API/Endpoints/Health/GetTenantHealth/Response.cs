namespace Rgt.Space.API.Endpoints.Health.GetTenantHealth;

public sealed record Response
{
    public string Status { get; init; } = string.Empty;
    public string TenantName { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public double Duration { get; init; }
    public DateTimeOffset Timestamp { get; init; }
}

