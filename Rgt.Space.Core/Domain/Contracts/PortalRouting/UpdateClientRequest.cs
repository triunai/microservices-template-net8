namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// Request DTO for updating an existing client.
/// </summary>
public sealed record UpdateClientRequest(
    string Name,
    string Code,
    string Status
);
