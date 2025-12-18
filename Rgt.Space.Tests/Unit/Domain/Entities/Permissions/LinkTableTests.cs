using Rgt.Space.Core.Domain.Entities.Permissions;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.Permissions;

public class RolePermissionTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var roleId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();

        // Act
        var rp = RolePermission.Create(roleId, permissionId);

        // Assert
        rp.Should().NotBeNull();
        rp.RoleId.Should().Be(roleId);
        rp.PermissionId.Should().Be(permissionId);
    }
}

public class UserRoleTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();

        // Act
        var ur = UserRole.Create(userId, roleId);

        // Assert
        ur.Should().NotBeNull();
        ur.UserId.Should().Be(userId);
        ur.RoleId.Should().Be(roleId);
    }
}

public class UserPermissionOverrideTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var permissionId = Guid.NewGuid();
        var isAllowed = true;

        // Act
        var overrideObj = UserPermissionOverride.Create(userId, permissionId, isAllowed);

        // Assert
        overrideObj.Should().NotBeNull();
        overrideObj.UserId.Should().Be(userId);
        overrideObj.PermissionId.Should().Be(permissionId);
        overrideObj.IsAllowed.Should().Be(isAllowed);
    }
}
