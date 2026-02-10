using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Services.Features;

namespace Rgt.Space.Tests.Unit.Services;

[Trait("Category", "Unit")]
public class FeatureGateTests : IDisposable
{
    private readonly IFeatureReadDac _readDac;
    private readonly MemoryCache _cache;
    private readonly ILogger<FeatureGate> _logger;
    private readonly FeatureGate _sut;

    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid TestClientId = Guid.Parse("019ac92a-0002-7000-0000-000000000002");
    private static readonly Guid TestUserId = Guid.Parse("019ac92a-0003-7000-0000-000000000003");
    private const string TestFeatureCode = "DASHBOARD_V2";

    public FeatureGateTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _logger = Substitute.For<ILogger<FeatureGate>>();
        _sut = new FeatureGate(_readDac, _cache, _logger);
    }

    public void Dispose() => _cache.Dispose();

    // --- Helpers ---

    private FeatureReadModel CreateFeatureModel(bool isActive, bool requiresClient) =>
        new(TestFeatureId, TestFeatureCode, "Dashboard V2", null,
            isActive, requiresClient, DateTime.UtcNow, null, DateTime.UtcNow, null);

    private ClientFeatureReadModel CreateClientFeatureModel(bool isEnabled) =>
        new(Guid.NewGuid(), TestClientId, TestFeatureId, isEnabled,
            DateTime.UtcNow, null, DateTime.UtcNow, null);

    private UserOverrideReadModel CreateUserOverrideModel(string overrideState) =>
        new(Guid.NewGuid(), TestUserId, TestFeatureId, overrideState,
            null, DateTime.UtcNow, null);

    private void SetupFeature(bool isActive, bool requiresClient) =>
        _readDac.GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>())
            .Returns(CreateFeatureModel(isActive, requiresClient));

    private void SetupClientFeature(bool isEnabled) =>
        _readDac.GetClientFeatureAsync(TestClientId, TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateClientFeatureModel(isEnabled));

    private void SetupNoClientFeature() =>
        _readDac.GetClientFeatureAsync(TestClientId, TestFeatureId, Arg.Any<CancellationToken>())
            .Returns((ClientFeatureReadModel?)null);

    private void SetupUserOverride(string overrideState) =>
        _readDac.GetUserOverrideAsync(TestUserId, TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateUserOverrideModel(overrideState));

    // --- IsEnabledAsync: Feature Lookup ---

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenFeatureNotFound()
    {
        // Arrange
        _readDac.GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.FeatureNotFound);
        result.FeatureCode.Should().Be(TestFeatureCode);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldNormalizeFeatureCode()
    {
        // Arrange
        _readDac.GetByCodeAsync("DASHBOARD_V2", Arg.Any<CancellationToken>())
            .Returns(CreateFeatureModel(isActive: true, requiresClient: false));

        // Act
        var result = await _sut.IsEnabledAsync("  dashboard_v2  ", Guid.Empty);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.FeatureCode.Should().Be("DASHBOARD_V2");
    }

    // --- IsEnabledAsync: Global Gate ---

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenGlobalOff()
    {
        // Arrange
        SetupFeature(isActive: false, requiresClient: true);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GlobalOff);
    }

    // --- IsEnabledAsync: Client Gate ---

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenInvalidClientId()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.InvalidClientId);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenClientNotSubscribed()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupNoClientFeature();

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.ClientNotSubscribed);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenClientOff()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: false);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.ClientOff);
    }

    // --- IsEnabledAsync: Client ON, User Overrides ---

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOn_WhenClientOnAndNoUserOverride()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: true);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedClient);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenUserForceOff()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: true);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOff);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.UserForceOff);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOn_WhenUserForceOn()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: true);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOn);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.UserForceOn);
    }

    // --- IsEnabledAsync: System Features (requiresClient = false) ---

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOn_WhenSystemFeatureAndGlobalOn()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: false);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedSystem);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenSystemFeatureAndUserForceOff()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: false);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOff);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty, TestUserId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.UserForceOff);
    }

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOn_WhenSystemFeatureAndUserForceOn()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: false);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOn);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty, TestUserId);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.UserForceOn);
    }

    // --- IsEnabledAsync: CRITICAL Row 9 ---

    [Fact]
    public async Task IsEnabledAsync_ShouldReturnOff_WhenClientOffEvenWithUserForceOn()
    {
        // Arrange — CRITICAL: FORCE_ON cannot bypass CLIENT_OFF (truth table row 9)
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: false);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOn);

        // Act
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.ClientOff);
    }

    // --- IsEnabledAsync: Edge Cases ---

    [Fact]
    public async Task IsEnabledAsync_ShouldSkipUserOverride_WhenUserIdIsGuidEmpty()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: true);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOff);

        // Act — userId = Guid.Empty should skip user override check
        var result = await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, Guid.Empty);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedClient);
    }

    // --- IsEnabledGloballyAsync ---

    [Fact]
    public async Task IsEnabledGloballyAsync_ShouldReturnOff_WhenFeatureNotFound()
    {
        // Arrange
        _readDac.GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.IsEnabledGloballyAsync(TestFeatureCode);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.FeatureNotFound);
    }

    [Fact]
    public async Task IsEnabledGloballyAsync_ShouldReturnOff_WhenRequiresClient()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);

        // Act
        var result = await _sut.IsEnabledGloballyAsync(TestFeatureCode);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.RequiresClient);
    }

    [Fact]
    public async Task IsEnabledGloballyAsync_ShouldReturnOff_WhenGlobalOff()
    {
        // Arrange
        SetupFeature(isActive: false, requiresClient: false);

        // Act
        var result = await _sut.IsEnabledGloballyAsync(TestFeatureCode);

        // Assert
        result.IsEnabled.Should().BeFalse();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GlobalOff);
    }

    [Fact]
    public async Task IsEnabledGloballyAsync_ShouldReturnOn_WhenActiveSystemFeature()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: false);

        // Act
        var result = await _sut.IsEnabledGloballyAsync(TestFeatureCode);

        // Assert
        result.IsEnabled.Should().BeTrue();
        result.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedSystem);
    }

    // --- Cache Invalidation ---

    [Fact]
    public async Task InvalidateFeature_ShouldClearCache_ForcingDacRefetch()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: false);

        // Act — first call populates cache
        await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty);
        // Act — second call should use cache
        await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty);

        // Assert — DAC called only once (second call used cache)
        await _readDac.Received(1).GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>());

        // Act — invalidate and call again
        _sut.InvalidateFeature(TestFeatureCode);
        await _sut.IsEnabledAsync(TestFeatureCode, Guid.Empty);

        // Assert — DAC called twice total (cache was invalidated)
        await _readDac.Received(2).GetByCodeAsync(TestFeatureCode, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateClientFeature_ShouldClearCache_ForcingDacRefetch()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: true);

        // Act — first call populates cache
        await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);
        // Act — second call should use cache
        await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert — client feature DAC called only once
        await _readDac.Received(1).GetClientFeatureAsync(
            TestClientId, TestFeatureId, Arg.Any<CancellationToken>());

        // Act — invalidate client feature and call again
        _sut.InvalidateClientFeature(TestClientId, TestFeatureId);
        await _sut.IsEnabledAsync(TestFeatureCode, TestClientId);

        // Assert — client feature DAC called twice total
        await _readDac.Received(2).GetClientFeatureAsync(
            TestClientId, TestFeatureId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateUserOverride_ShouldClearCache_ForcingDacRefetch()
    {
        // Arrange
        SetupFeature(isActive: true, requiresClient: true);
        SetupClientFeature(isEnabled: true);
        SetupUserOverride(FeatureFlagConstants.OverrideStates.ForceOn);

        // Act — first call populates cache
        await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId);
        // Act — second call should use cache
        await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId);

        // Assert — user override DAC called only once
        await _readDac.Received(1).GetUserOverrideAsync(
            TestUserId, TestFeatureId, Arg.Any<CancellationToken>());

        // Act — invalidate user override and call again
        _sut.InvalidateUserOverride(TestUserId, TestFeatureId);
        await _sut.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId);

        // Assert — user override DAC called twice total
        await _readDac.Received(2).GetUserOverrideAsync(
            TestUserId, TestFeatureId, Arg.Any<CancellationToken>());
    }
}
