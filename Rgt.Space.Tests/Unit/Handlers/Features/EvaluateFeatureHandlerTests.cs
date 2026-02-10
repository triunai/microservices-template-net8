using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class EvaluateFeatureHandlerTests
{
    private readonly IFeatureGate _featureGate;
    private readonly EvaluateFeature.Handler _sut;

    private static readonly Guid TestUserId = Guid.Parse("019ac92a-0003-7000-0000-000000000003");
    private static readonly Guid TestClientId = Guid.Parse("019ac92a-0002-7000-0000-000000000002");
    private const string TestFeatureCode = "DASHBOARD_V2";

    public EvaluateFeatureHandlerTests()
    {
        _featureGate = Substitute.For<IFeatureGate>();
        _sut = new EvaluateFeature.Handler(_featureGate);
    }

    [Fact]
    public async Task Handle_ShouldReturnFeatureDecision_WhenCalled()
    {
        // Arrange
        var expectedDecision = new FeatureDecision(true, FeatureFlagConstants.ReasonCodes.GrantedClient, TestFeatureCode);
        var query = new EvaluateFeature.Query(TestFeatureCode, TestUserId, TestClientId);

        _featureGate.IsEnabledAsync(TestFeatureCode, TestClientId, TestUserId, Arg.Any<CancellationToken>())
            .Returns(expectedDecision);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsEnabled.Should().BeTrue();
        result.Value.ReasonCode.Should().Be(FeatureFlagConstants.ReasonCodes.GrantedClient);
        result.Value.FeatureCode.Should().Be(TestFeatureCode);
    }

    [Fact]
    public async Task Handle_ShouldPassGuidEmpty_WhenClientIdIsNull()
    {
        // Arrange
        var expectedDecision = new FeatureDecision(true, FeatureFlagConstants.ReasonCodes.GrantedSystem, TestFeatureCode);
        var query = new EvaluateFeature.Query(TestFeatureCode, TestUserId, ClientId: null);

        _featureGate.IsEnabledAsync(
                TestFeatureCode,
                Arg.Is<Guid>(g => g == Guid.Empty),
                TestUserId,
                Arg.Any<CancellationToken>())
            .Returns(expectedDecision);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _featureGate.Received(1).IsEnabledAsync(
            TestFeatureCode,
            Arg.Is<Guid>(g => g == Guid.Empty),
            TestUserId,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldPassClientId_WhenProvided()
    {
        // Arrange
        var expectedDecision = new FeatureDecision(true, FeatureFlagConstants.ReasonCodes.GrantedClient, TestFeatureCode);
        var query = new EvaluateFeature.Query(TestFeatureCode, TestUserId, TestClientId);

        _featureGate.IsEnabledAsync(
                TestFeatureCode,
                TestClientId,
                TestUserId,
                Arg.Any<CancellationToken>())
            .Returns(expectedDecision);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _featureGate.Received(1).IsEnabledAsync(
            TestFeatureCode,
            Arg.Is<Guid>(g => g == TestClientId),
            TestUserId,
            Arg.Any<CancellationToken>());
    }
}
