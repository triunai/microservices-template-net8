using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class BulkEvaluateFeaturesHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly BulkEvaluateFeatures.Handler _sut;

    private static readonly Guid TestUserId = Guid.Parse("019ac92a-0003-7000-0000-000000000003");
    private static readonly Guid TestClientId = Guid.Parse("019ac92a-0002-7000-0000-000000000002");
    private static readonly Guid FeatureId1 = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid FeatureId2 = Guid.Parse("019ac92a-0001-7000-0000-000000000002");

    public BulkEvaluateFeaturesHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new BulkEvaluateFeatures.Handler(_readDac);
    }

    // --- Helpers ---

    private static FeatureReadModel CreateFeature(Guid id, string code, bool isActive, bool requiresClient) =>
        new(id, code, $"Feature {code}", null,
            isActive, requiresClient, DateTime.UtcNow, null, DateTime.UtcNow, null);

    private static ClientFeatureReadModel CreateClientFeature(Guid featureId, bool isEnabled) =>
        new(Guid.NewGuid(), TestClientId, featureId, isEnabled,
            DateTime.UtcNow, null, DateTime.UtcNow, null);

    private void SetupTwoFeatures()
    {
        var features = new List<FeatureReadModel>
        {
            CreateFeature(FeatureId1, "DASHBOARD_V2", isActive: true, requiresClient: true),
            CreateFeature(FeatureId2, "SYS_COMBO_BREAK", isActive: true, requiresClient: false)
        };

        _readDac.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(features);

        _readDac.GetUserOverridesByUserAsync(TestUserId, Arg.Any<CancellationToken>())
            .Returns(new List<UserOverrideReadModel>());
    }

    [Fact]
    public async Task Handle_ShouldReturnAllFeaturesEvaluated()
    {
        // Arrange
        SetupTwoFeatures();

        var clientSubs = new List<ClientFeatureReadModel>
        {
            CreateClientFeature(FeatureId1, isEnabled: true)
        };
        _readDac.GetClientFeaturesByClientAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(clientSubs);

        var query = new BulkEvaluateFeatures.Query(TestUserId, TestClientId);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Features.Should().HaveCount(2);
        result.Value.Features.Should().Contain(d => d.FeatureCode == "DASHBOARD_V2" && d.IsEnabled);
        result.Value.Features.Should().Contain(d => d.FeatureCode == "SYS_COMBO_BREAK" && d.IsEnabled);
    }

    [Fact]
    public async Task Handle_ShouldSkipClientFetch_WhenClientIdNull()
    {
        // Arrange
        SetupTwoFeatures();
        var query = new BulkEvaluateFeatures.Query(TestUserId, ClientId: null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _readDac.Received(0).GetClientFeaturesByClientAsync(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldFetchClientFeatures_WhenClientIdProvided()
    {
        // Arrange
        SetupTwoFeatures();

        _readDac.GetClientFeaturesByClientAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(new List<ClientFeatureReadModel>());

        var query = new BulkEvaluateFeatures.Query(TestUserId, TestClientId);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _readDac.Received(1).GetClientFeaturesByClientAsync(
            TestClientId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldReturnCorrectResponseShape()
    {
        // Arrange
        SetupTwoFeatures();

        _readDac.GetClientFeaturesByClientAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(new List<ClientFeatureReadModel>
            {
                CreateClientFeature(FeatureId1, isEnabled: true)
            });

        var beforeCall = DateTime.UtcNow;
        var query = new BulkEvaluateFeatures.Query(TestUserId, TestClientId);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(TestUserId);
        result.Value.ClientId.Should().Be(TestClientId);
        result.Value.EvaluatedAt.Should().BeOnOrAfter(beforeCall);
        result.Value.Features.Should().NotBeNull();
    }
}
