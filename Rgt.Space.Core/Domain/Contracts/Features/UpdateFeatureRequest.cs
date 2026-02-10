namespace Rgt.Space.Core.Domain.Contracts.Features;

public sealed record UpdateFeatureRequest(
    string Name,
    string? Description,
    bool IsActive,
    bool RequiresClient);
