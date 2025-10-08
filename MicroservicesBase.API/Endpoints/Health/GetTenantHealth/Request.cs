namespace MicroservicesBase.API.Endpoints.Health.GetTenantHealth;

public sealed record Request
{
    /// <summary>
    /// Tenant name (e.g., "7ELEVEN", "BURGERKING"), not the GUID Id
    /// </summary>
    public string TenantName { get; init; } = string.Empty;
}

