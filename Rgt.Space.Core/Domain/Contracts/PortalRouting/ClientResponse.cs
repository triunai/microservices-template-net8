namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// API response for Client entity.
/// Used in Portal Routing module for client navigation.
/// </summary>
public sealed record ClientResponse(
    Guid Id,
    string Name,
    string Code,
    string Status,
    DateTime CreatedAt
);
