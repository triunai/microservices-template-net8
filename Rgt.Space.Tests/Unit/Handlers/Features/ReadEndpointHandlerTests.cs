using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class GetClientSubscriptionsHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly GetClientSubscriptions.Handler _sut;

    public GetClientSubscriptionsHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new GetClientSubscriptions.Handler(_readDac);
    }

    [Fact]
    public async Task Handle_ShouldReturnClientSubscriptions()
    {
        // Arrange
        var featureId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var subscriptions = new List<ClientFeatureDetailReadModel>
        {
            new(Guid.NewGuid(), clientId, "Acme Corp", "ACME", featureId, true, DateTime.UtcNow, DateTime.UtcNow),
            new(Guid.NewGuid(), Guid.NewGuid(), "Beta Inc", "BETA", featureId, false, DateTime.UtcNow, DateTime.UtcNow)
        };

        _readDac.GetClientSubscriptionsByFeatureAsync(featureId, Arg.Any<CancellationToken>())
            .Returns(subscriptions);

        // Act
        var result = await _sut.Handle(new GetClientSubscriptions.Query(featureId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(s => s.ClientCode == "ACME");
        result.Value.Should().Contain(s => s.ClientCode == "BETA");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoSubscriptions()
    {
        // Arrange
        var featureId = Guid.NewGuid();
        _readDac.GetClientSubscriptionsByFeatureAsync(featureId, Arg.Any<CancellationToken>())
            .Returns(new List<ClientFeatureDetailReadModel>());

        // Act
        var result = await _sut.Handle(new GetClientSubscriptions.Query(featureId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class GetClientFeaturesHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly GetClientFeatures.Handler _sut;

    public GetClientFeaturesHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new GetClientFeatures.Handler(_readDac);
    }

    [Fact]
    public async Task Handle_ShouldReturnClientFeatures()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var features = new List<ClientFeatureByClientReadModel>
        {
            new(Guid.NewGuid(), clientId, Guid.NewGuid(), "DASHBOARD_V2", "Dashboard V2", true, true, DateTime.UtcNow, DateTime.UtcNow),
            new(Guid.NewGuid(), clientId, Guid.NewGuid(), "DARK_MODE", "Dark Mode", false, true, DateTime.UtcNow, DateTime.UtcNow)
        };

        _readDac.GetFeaturesByClientAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(features);

        // Act
        var result = await _sut.Handle(new GetClientFeatures.Query(clientId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(f => f.FeatureCode == "DASHBOARD_V2");
        result.Value.Should().Contain(f => f.FeatureCode == "DARK_MODE");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoFeatures()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        _readDac.GetFeaturesByClientAsync(clientId, Arg.Any<CancellationToken>())
            .Returns(new List<ClientFeatureByClientReadModel>());

        // Act
        var result = await _sut.Handle(new GetClientFeatures.Query(clientId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class GetFeatureUserOverridesHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly GetFeatureUserOverrides.Handler _sut;

    public GetFeatureUserOverridesHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new GetFeatureUserOverrides.Handler(_readDac);
    }

    [Fact]
    public async Task Handle_ShouldReturnUserOverrides()
    {
        // Arrange
        var featureId = Guid.NewGuid();
        var overrides = new List<UserOverrideDetailReadModel>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), "John Doe", "john@example.com", featureId, "FORCE_ON", "Beta tester", DateTime.UtcNow, null),
            new(Guid.NewGuid(), Guid.NewGuid(), "Jane Smith", "jane@example.com", featureId, "FORCE_OFF", "Opt-out requested", DateTime.UtcNow, Guid.NewGuid())
        };

        _readDac.GetUserOverridesByFeatureAsync(featureId, Arg.Any<CancellationToken>())
            .Returns(overrides);

        // Act
        var result = await _sut.Handle(new GetFeatureUserOverrides.Query(featureId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(o => o.UserDisplayName == "John Doe");
        result.Value.Should().Contain(o => o.UserDisplayName == "Jane Smith");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoOverrides()
    {
        // Arrange
        var featureId = Guid.NewGuid();
        _readDac.GetUserOverridesByFeatureAsync(featureId, Arg.Any<CancellationToken>())
            .Returns(new List<UserOverrideDetailReadModel>());

        // Act
        var result = await _sut.Handle(new GetFeatureUserOverrides.Query(featureId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}

[Trait("Category", "Unit")]
public class GetUserOverridesHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly GetUserOverrides.Handler _sut;

    public GetUserOverridesHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new GetUserOverrides.Handler(_readDac);
    }

    [Fact]
    public async Task Handle_ShouldReturnUserOverrides()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var overrides = new List<UserOverrideByUserReadModel>
        {
            new(Guid.NewGuid(), userId, Guid.NewGuid(), "DASHBOARD_V2", "Dashboard V2", "FORCE_ON", "Beta tester", DateTime.UtcNow, null),
            new(Guid.NewGuid(), userId, Guid.NewGuid(), "DARK_MODE", "Dark Mode", "FORCE_OFF", null, DateTime.UtcNow, Guid.NewGuid())
        };

        _readDac.GetOverridesByUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(overrides);

        // Act
        var result = await _sut.Handle(new GetUserOverrides.Query(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(o => o.FeatureCode == "DASHBOARD_V2");
        result.Value.Should().Contain(o => o.FeatureCode == "DARK_MODE");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoOverrides()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _readDac.GetOverridesByUserAsync(userId, Arg.Any<CancellationToken>())
            .Returns(new List<UserOverrideByUserReadModel>());

        // Act
        var result = await _sut.Handle(new GetUserOverrides.Query(userId), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
