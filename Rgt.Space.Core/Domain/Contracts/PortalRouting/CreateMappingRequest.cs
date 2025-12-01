namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// Request DTO for creating a new client-project mapping.
/// </summary>
public sealed record CreateMappingRequest(
    Guid ProjectId,
    string RoutingUrl,
    string Environment
);
