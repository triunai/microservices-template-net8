using Rgt.Space.Core.Domain.Entities.Permissions;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.Permissions;

public class RoleTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var name = "Admin";
        var description = "Administrator";
        var isSystem = true;

        // Act
        var role = Role.Create(name, description, isSystem);

        // Assert
        role.Should().NotBeNull();
        role.Id.Should().NotBeEmpty();
        role.Name.Should().Be(name);
        role.Description.Should().Be(description);
        role.IsSystem.Should().Be(isSystem);
        role.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
