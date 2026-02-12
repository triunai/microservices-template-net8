using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Services.Features;

/// <summary>
/// Pure static evaluator — single source of truth for the feature flag decision tree.
/// Used by both FeatureGate (single, cached) and bulk evaluation handler (3-query, fresh).
/// No caching, no logging, no DB — just data in, decision out.
/// </summary>
public static class FeatureEvaluator
{
    public static FeatureDecision Evaluate(
        FeatureReadModel? feature,
        string featureCode,
        Guid clientId,
        ClientFeatureReadModel? clientFeature,
        UserOverrideReadModel? userOverride)
    {
        // Step 1: Feature lookup
        if (feature is null)
            return new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.FeatureNotFound, featureCode);

        // Step 2: Global gate — master kill switch
        if (!feature.IsActive)
            return new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.GlobalOff, featureCode);

        // Step 3: Client gate (skip for system features where requires_client = FALSE)
        if (feature.RequiresClient)
        {
            if (clientId == Guid.Empty)
                return new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.InvalidClientId, featureCode);

            if (clientFeature is null)
                return new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.ClientNotSubscribed, featureCode);

            // CRITICAL: FORCE_ON cannot bypass CLIENT_OFF (truth table row 9)
            if (!clientFeature.IsEnabled)
                return new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.ClientOff, featureCode);
        }

        // Step 4: User override
        if (userOverride is not null)
        {
            if (userOverride.OverrideState == FeatureFlagConstants.OverrideStates.ForceOff)
                return new FeatureDecision(false, FeatureFlagConstants.ReasonCodes.UserForceOff, featureCode);

            if (userOverride.OverrideState == FeatureFlagConstants.OverrideStates.ForceOn)
                return new FeatureDecision(true, FeatureFlagConstants.ReasonCodes.UserForceOn, featureCode);
        }

        // Step 5: All gates passed — reason depends on feature scope
        var reasonCode = feature.RequiresClient
            ? FeatureFlagConstants.ReasonCodes.GrantedClient
            : FeatureFlagConstants.ReasonCodes.GrantedSystem;

        return new FeatureDecision(true, reasonCode, featureCode);
    }
}
