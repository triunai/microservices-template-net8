using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Commands.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class UpdateFeatureHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly IFeatureWriteDac _writeDac;
    private readonly IFeatureGate _featureGate;
    private readonly UpdateFeature.Handler _sut;

    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid TestUpdatedBy = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");
    private const string TestFeatureCode = "DASHBOARD_V2";

    public UpdateFeatureHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _writeDac = Substitute.For<IFeatureWriteDac>();
        _featureGate = Substitute.For<IFeatureGate>();
        _sut = new UpdateFeature.Handler(_readDac, _writeDac, _featureGate);
    }

    private FeatureReadModel CreateTestFeature() =>
        new(TestFeatureId, TestFeatureCode, "Dashboard V2", null,
            true, true, DateTime.UtcNow, null, DateTime.UtcNow, null);

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenFeatureNotFound()
    {
        // Arrange
        var command = new UpdateFeature.Command(
            TestFeatureId, "Updated Name", null, true, true, TestUpdatedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnOk_WhenValid()
    {
        // Arrange
        var command = new UpdateFeature.Command(
            TestFeatureId, "Updated Name", "New description", false, true, TestUpdatedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());
        _writeDac.UpdateFeatureAsync(
            TestFeatureId, Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<bool>(), TestUpdatedBy, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _writeDac.Received(1).UpdateFeatureAsync(
            TestFeatureId, "Updated Name", "New description",
            false, true, TestUpdatedBy, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldInvalidateCache_WhenSuccessful()
    {
        // Arrange
        var command = new UpdateFeature.Command(
            TestFeatureId, "Updated Name", null, true, true, TestUpdatedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());
        _writeDac.UpdateFeatureAsync(
            TestFeatureId, Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<bool>(), TestUpdatedBy, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        await _sut.Handle(command, CancellationToken.None);

        // Assert
        _featureGate.Received(1).InvalidateFeature(TestFeatureCode);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenConcurrentlyDeleted()
    {
        // Arrange — feature exists at read time but is deleted before write lands (TOCTOU race)
        var command = new UpdateFeature.Command(
            TestFeatureId, "Updated Name", null, true, true, TestUpdatedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());
        _writeDac.UpdateFeatureAsync(
            TestFeatureId, Arg.Any<string>(), Arg.Any<string?>(),
            Arg.Any<bool>(), Arg.Any<bool>(), TestUpdatedBy, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
        _featureGate.DidNotReceive().InvalidateFeature(Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenNameIsEmpty()
    {
        // Arrange
        var command = new UpdateFeature.Command(
            TestFeatureId, "", null, true, true, TestUpdatedBy);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message == "FEATURE_NAME_REQUIRED");
    }
}
