using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Commands.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class SoftDeleteFeatureHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly IFeatureWriteDac _writeDac;
    private readonly IFeatureGate _featureGate;
    private readonly SoftDeleteFeature.Handler _sut;

    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");
    private static readonly Guid TestDeletedBy = Guid.Parse("019ac92a-de20-7793-b8df-b88a87ea4e34");
    private const string TestFeatureCode = "DASHBOARD_V2";

    public SoftDeleteFeatureHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _writeDac = Substitute.For<IFeatureWriteDac>();
        _featureGate = Substitute.For<IFeatureGate>();
        _sut = new SoftDeleteFeature.Handler(_readDac, _writeDac, _featureGate);
    }

    private FeatureReadModel CreateTestFeature() =>
        new(TestFeatureId, TestFeatureCode, "Dashboard V2", null,
            true, true, DateTime.UtcNow, null, DateTime.UtcNow, null);

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenFeatureNotFound()
    {
        // Arrange
        var command = new SoftDeleteFeature.Command(TestFeatureId, TestDeletedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnOkAndInvalidateCache_WhenValid()
    {
        // Arrange
        var command = new SoftDeleteFeature.Command(TestFeatureId, TestDeletedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());
        _writeDac.SoftDeleteFeatureAsync(TestFeatureId, TestDeletedBy, Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        await _writeDac.Received(1).SoftDeleteFeatureAsync(
            TestFeatureId, TestDeletedBy, Arg.Any<CancellationToken>());
        _featureGate.Received(1).InvalidateFeature(TestFeatureCode);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenConcurrentlyDeleted()
    {
        // Arrange — feature exists at read time but is already deleted by write time (TOCTOU race)
        var command = new SoftDeleteFeature.Command(TestFeatureId, TestDeletedBy);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(CreateTestFeature());
        _writeDac.SoftDeleteFeatureAsync(TestFeatureId, TestDeletedBy, Arg.Any<CancellationToken>())
            .Returns(false);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
        _featureGate.DidNotReceive().InvalidateFeature(Arg.Any<string>());
    }
}
