using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Features;

namespace Rgt.Space.Tests.Unit.Handlers.Features;

[Trait("Category", "Unit")]
public class GetAllFeaturesHandlerTests
{
    private readonly IFeatureReadDac _readDac;
    private readonly GetAllFeatures.Handler _sut;

    public GetAllFeaturesHandlerTests()
    {
        _readDac = Substitute.For<IFeatureReadDac>();
        _sut = new GetAllFeatures.Handler(_readDac);
    }

    [Fact]
    public async Task Handle_ShouldReturnAllFeatures()
    {
        // Arrange
        var features = new List<FeatureReadModel>
        {
            new(Guid.NewGuid(), "DASHBOARD_V2", "Dashboard V2", null,
                true, true, DateTime.UtcNow, null, DateTime.UtcNow, null),
            new(Guid.NewGuid(), "SYS_COMBO_BREAK_DEBUG", "Combo Break Debug", null,
                true, false, DateTime.UtcNow, null, DateTime.UtcNow, null)
        };

        _readDac.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(features);

        // Act
        var result = await _sut.Handle(new GetAllFeatures.Query(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(f => f.Code == "DASHBOARD_V2");
        result.Value.Should().Contain(f => f.Code == "SYS_COMBO_BREAK_DEBUG");
    }

    [Fact]
    public async Task Handle_ShouldReturnEmptyList_WhenNoFeatures()
    {
        // Arrange
        _readDac.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<FeatureReadModel>());

        // Act
        var result = await _sut.Handle(new GetAllFeatures.Query(), CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }
}
