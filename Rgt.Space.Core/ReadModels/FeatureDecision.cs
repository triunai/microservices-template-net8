namespace Rgt.Space.Core.ReadModels;

public sealed record FeatureDecision(
    bool IsEnabled,
    string ReasonCode,
    string FeatureCode);
