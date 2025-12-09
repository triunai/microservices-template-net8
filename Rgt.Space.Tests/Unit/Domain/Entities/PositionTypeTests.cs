using Rgt.Space.Core.Domain.Entities.TaskAllocation;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Tests.Unit.Domain.Entities;

public class PositionTypeTests
{
    [Fact]
    public void Create_ShouldInitializeWithDefaultStatusActive()
    {
        // Arrange
        var code = "TECH_LEAD";
        var name = "Technical Lead";
        var sortOrder = 1;

        // Act
        var positionType = PositionType.Create(code, name, sortOrder);

        // Assert
        positionType.Should().NotBeNull();
        positionType.Code.Should().Be(code);
        positionType.Name.Should().Be(name);
        positionType.SortOrder.Should().Be(sortOrder);
        positionType.Status.Should().Be(StatusConstants.Active); // Default should be Active
        positionType.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        positionType.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_ShouldAcceptCustomStatus()
    {
        // Arrange
        var code = "RETIRED_ROLE";
        var name = "Old Role";
        var sortOrder = 99;
        var status = StatusConstants.Inactive;

        // Act
        var positionType = PositionType.Create(code, name, sortOrder, status: status);

        // Assert
        positionType.Status.Should().Be(StatusConstants.Inactive);
    }
}
