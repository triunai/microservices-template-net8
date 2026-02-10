namespace Rgt.Space.Core.Domain.Contracts.Features;

public sealed record CreateFeatureRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive = false,
    bool RequiresClient = true);
