using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Services.Features;

namespace Rgt.Space.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class FeatureEvaluatorTests
{
    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid TestClientId = Guid.Parse("019ac92a-0002-7000-0000-000000000002");
    private static readonly Guid TestUserId = Guid.Parse("019ac92a-0003-7000-0000-000000000003");
    private const string TestFeatureCode = "EVAL_TEST";

    // --- Helpers ---

    private static FeatureReadModel CreateFeature(bool isActive, bool requiresClient) =>
        new(TestFeatureId, TestFeatureCode, "Eval Test", null,
            isActive, requiresClient, DateTime.UtcNow, null, DateTime.UtcNow, null);

    private static ClientFeatureReadModel CreateClientFeature(bool isEnabled) =>
        new(Guid.NewGuid(), TestClientId, TestFeatureId, isEnabled,
            DateTime.UtcNow, null, DateTime.UtcNow, null);

    private static UserOverrideReadModel CreateUserOverride(string overrideState) =>
        new(Guid.NewGuid(), TestUserId, TestFeatureId, overrideState,
            null, DateTime.UtcNow, null);

    // --- Step 1: Feature Lookup ---

    [Fact]
    public void Evaluate_ShouldReturnFeatureNotFound_WhenFeatureIsNull()
    {
        // Arrange
        FeatureReadModel? feature = null;

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, null, null);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.FeatureNotFound);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    // --- Step 2: Global Gate ---

    [Fact]
    public void Evaluate_ShouldReturnGlobalOff_WhenFeatureNotActive()
    {
        // Arrange
        var feature = CreateFeature(isActive: false, requiresClient: true);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, null, null);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GlobalOff);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    // --- Step 3: Client Gate ---

    [Fact]
    public void Evaluate_ShouldReturnInvalidClientId_WhenRequiresClientAndClientIdEmpty()
    {
        // Arrange
        var feature = CreateFeature(isActive: true, requiresClient: true);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, Guid.Empty, null, null);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.InvalidClientId);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    [Fact]
    public void Evaluate_ShouldReturnClientNotSubscribed_WhenNoClientFeature()
    {
        // Arrange
        var feature = CreateFeature(isActive: true, requiresClient: true);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, null, null);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.ClientNotSubscribed);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    [Fact]
    public void Evaluate_ShouldReturnClientOff_WhenClientFeatureDisabled()
    {
        // Arrange
        var feature = CreateFeature(isActive: true, requiresClient: true);
        var clientFeature = CreateClientFeature(isEnabled: false);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, clientFeature, null);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.ClientOff);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    // --- Step 4: User Override ---

    [Fact]
    public void Evaluate_ShouldReturnUserForceOff_WhenOverrideForceOff()
    {
        // Arrange
        var feature = CreateFeature(isActive: true, requiresClient: true);
        var clientFeature = CreateClientFeature(isEnabled: true);
        var userOverride = CreateUserOverride(FeatureFlagConstants.OverrideStates.ForceOff);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, clientFeature, userOverride);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.UserForceOff);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    [Fact]
    public void Evaluate_ShouldReturnUserForceOn_WhenOverrideForceOn()
    {
        // Arrange
        var feature = CreateFeature(isActive: true, requiresClient: true);
        var clientFeature = CreateClientFeature(isEnabled: true);
        var userOverride = CreateUserOverride(FeatureFlagConstants.OverrideStates.ForceOn);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, clientFeature, userOverride);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.UserForceOn);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    // --- Step 5: All Gates Passed ---

    [Fact]
    public void Evaluate_ShouldReturnGrantedSystem_WhenSystemFeatureAllGatesPassed()
    {
        // Arrange — requiresClient=false, isActive=true, no override
        var feature = CreateFeature(isActive: true, requiresClient: false);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, Guid.Empty, null, null);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedSystem);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    [Fact]
    public void Evaluate_ShouldReturnGrantedClient_WhenClientFeatureAllGatesPassed()
    {
        // Arrange — requiresClient=true, clientEnabled=true, no override
        var feature = CreateFeature(isActive: true, requiresClient: true);
        var clientFeature = CreateClientFeature(isEnabled: true);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, clientFeature, null);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedClient);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    // --- CRITICAL: Truth Table Row 9 ---

    [Fact]
    public void Evaluate_ShouldReturnClientOff_WhenClientOffEvenWithUserForceOn()
    {
        // Arrange — CRITICAL: FORCE_ON cannot bypass CLIENT_OFF (truth table row 9)
        var feature = CreateFeature(isActive: true, requiresClient: true);
        var clientFeature = CreateClientFeature(isEnabled: false);
        var userOverride = CreateUserOverride(FeatureFlagConstants.OverrideStates.ForceOn);

        // Act
        var result = FeatureEvaluator.Evaluate(feature, TestFeatureCode, TestClientId, clientFeature, userOverride);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.ClientOff);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }
}
