namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// Request DTO for creating a new client.
/// </summary>
public sealed record CreateClientRequest(
    string Name,
    string Code,
    string Status
);
