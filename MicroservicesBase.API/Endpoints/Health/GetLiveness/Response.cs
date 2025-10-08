namespace MicroservicesBase.API.Endpoints.Health.GetLiveness;

public sealed record Response
{
    public string Status { get; init; } = string.Empty;
    public double TotalDuration { get; init; }
    public string Message { get; init; } = string.Empty;
}

