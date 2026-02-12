using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Commands.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class UpsertClientFeatureHandlerTests
{
    private readonly IFeatureReadDac _featureReadDac;
    private readonly IFeatureWriteDac _featureWriteDac;
    private readonly IClientReadDac _clientReadDac;
    private readonly IFeatureGate _featureGate;
    private readonly UpsertClientFeature.Handler _sut;

    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid TestClientId = Guid.Parse("019ac92a-0002-7000-0000-000000000002");
    private static readonly Guid TestUpdatedBy = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");

    public UpsertClientFeatureHandlerTests()
    {
        _featureReadDac = Substitute.For<IFeatureReadDac>();
        _featureWriteDac = Substitute.For<IFeatureWriteDac>();
        _clientReadDac = Substitute.For<IClientReadDac>();
        _featureGate = Substitute.For<IFeatureGate>();
        _sut = new UpsertClientFeature.Handler(
            _featureReadDac, _featureWriteDac, _clientReadDac, _featureGate);
    }

    private FeatureReadModel CreateTestFeature() =>
        new(TestFeatureId, "DASHBOARD_V2", "Dashboard V2", null,
            true, true, DateTime.UtcNow, null, DateTime.UtcNow, null);

    private ClientReadModel CreateTestClient() =>
        new(TestClientId, "Test Client", "TEST_CLIENT", "Active",
            DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenFeatureNotFound()
    {
        // Arrange
        var command = new UpsertClientFeature.Command(
            TestClientId, TestFeatureId, true, TestUpdatedBy);

        _featureReadDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenClientNotFound()
    {
        // Arrange
        var command = new UpsertClientFeature.Command(
            TestClientId, TestFeatureId, true, TestUpdatedBy);

        _featureReadDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());

        _clientReadDac.GetByIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns((ClientReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.CLIENT_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnOkAndInvalidateCache_WhenValid()
    {
        // Arrange
        var command = new UpsertClientFeature.Command(
            TestClientId, TestFeatureId, true, TestUpdatedBy);

        _featureReadDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());

        _clientReadDac.GetByIdAsync(TestClientId, Arg.Any<CancellationToken>())
            .Returns(CreateTestClient());

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _featureWriteDac.Received(1).UpsertClientFeatureAsync(
            TestClientId, TestFeatureId, true, TestUpdatedBy,
            Arg.Any<CancellationToken>());
        _featureGate.Received(1).InvalidateClientFeature(TestClientId, TestFeatureId);
    }
}
