namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// Request DTO for updating an existing client-project mapping.
/// </summary>
public sealed record UpdateMappingRequest(
    string RoutingUrl,
    string Environment,
    string Status
);
