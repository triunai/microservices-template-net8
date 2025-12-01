namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// API response for Client-Project Mapping (Admin Console view).
/// Shows the complete routing configuration with denormalized client/project info.
/// </summary>
public sealed record ClientProjectMappingResponse(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    Guid ClientId,
    string ClientName,
    string ClientCode,
    string RoutingUrl,
    string Environment,
    string Status,
    DateTime CreatedAt
);
