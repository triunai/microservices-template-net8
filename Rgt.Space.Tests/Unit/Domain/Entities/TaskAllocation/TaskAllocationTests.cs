using Rgt.Space.Core.Domain.Entities.TaskAllocation;
using Rgt.Space.Core.Constants;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.TaskAllocation;

public class PositionTypeTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var code = "DEV";
        var name = "Developer";
        var sortOrder = 1;
        var description = "Software Developer";
        var status = StatusConstants.Active;

        // Act
        var position = PositionType.Create(code, name, sortOrder, description, status);

        // Assert
        position.Should().NotBeNull();
        position.Code.Should().Be(code);
        position.Name.Should().Be(name);
        position.SortOrder.Should().Be(sortOrder);
        position.Description.Should().Be(description);
        position.Status.Should().Be(status);
        position.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}

public class ProjectAssignmentTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var positionCode = "DEV";

        // Act
        var assignment = ProjectAssignment.Create(projectId, userId, positionCode);

        // Assert
        assignment.Should().NotBeNull();
        assignment.ProjectId.Should().Be(projectId);
        assignment.UserId.Should().Be(userId);
        assignment.PositionCode.Should().Be(positionCode);
        assignment.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
