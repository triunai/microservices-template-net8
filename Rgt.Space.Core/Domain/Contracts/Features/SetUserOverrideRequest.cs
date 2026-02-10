namespace Rgt.Space.Core.Domain.Contracts.Features;

public sealed record SetUserOverrideRequest(
    string FeatureCode,
    string OverrideState,
    string? Reason);
