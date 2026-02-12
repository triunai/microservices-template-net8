using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.Features;

public interface IFeatureGate
{
    /// <summary>
    /// Evaluate feature for a specific client + optional user override.
    /// Use for client-specific operations (most common case).
    /// </summary>
    Task<FeatureDecision> IsEnabledAsync(
        string featureCode,
        Guid clientId,
        Guid? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate global-only feature (requires_client = FALSE).
    /// Use for system features like SYS_COMBO_BREAK_DEBUG.
    /// Returns OFF with REQUIRES_CLIENT if feature.requires_client = TRUE.
    /// </summary>
    Task<FeatureDecision> IsEnabledGloballyAsync(
        string featureCode,
        CancellationToken ct = default);

    /// <summary>
    /// Invalidate cached feature row. Call after admin toggles global is_active.
    /// Instance-local only — other nodes propagate within cache TTL.
    /// </summary>
    void InvalidateFeature(string featureCode);

    /// <summary>
    /// Invalidate cached client subscription. Call after admin toggles client feature.
    /// </summary>
    void InvalidateClientFeature(Guid clientId, Guid featureId);

    /// <summary>
    /// Invalidate cached user override. Call after admin sets/clears override.
    /// </summary>
    void InvalidateUserOverride(Guid userId, Guid featureId);
}
