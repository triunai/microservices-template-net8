using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class GetFeatureByIdHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly GetFeatureById.Handler _sut;

    private static readonly Guid TestFeatureId = Guid.Parse("019ac92a-0001-7000-0000-000000000001");

    public GetFeatureByIdHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new GetFeatureById.Handler(_readDac);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenFeatureNotFound()
    {
        // Arrange
        var query = new GetFeatureById.Query(TestFeatureId);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns((FeatureReadModel?)null);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().ContainSingle(e => e.Message == ErrorCatalog.FEATURE_NOT_FOUND);
    }

    [Fact]
    public async Task Handle_ShouldReturnOk_WhenFeatureExists()
    {
        // Arrange
        var expected = new FeatureReadModel(
            TestFeatureId, "DASHBOARD_V2", "Dashboard V2", null,
            true, true, DateTime.UtcNow, null, DateTime.UtcNow, null);

        var query = new GetFeatureById.Query(TestFeatureId);

        _readDac.GetByIdAsync(TestFeatureId, Arg.Any<CancellationToken>())
            .Returns(expected);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_ShouldReturnFail_WhenFeatureIdIsEmpty()
    {
        // Arrange
        var query = new GetFeatureById.Query(Guid.Empty);

        // Act
        var result = await _sut.Handle(query, CancellationToken.None);

        // Assert
        result.IsFailed.Should().BeTrue();
        result.Errors.Should().Contain(e => e.Message == "FEATURE_ID_REQUIRED");
    }
}
