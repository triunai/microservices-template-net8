namespace Rgt.Space.Core.Domain.Contracts.PortalRouting;

/// <summary>
/// API response for Project entity with client context.
/// </summary>
public sealed record ProjectResponse(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string Name,
    string Code,
    string? ExternalUrl,
    string Status,
    DateTime CreatedAt
);
