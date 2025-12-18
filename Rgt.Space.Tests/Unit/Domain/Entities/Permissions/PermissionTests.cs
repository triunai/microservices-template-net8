using Rgt.Space.Core.Domain.Entities.Permissions;
using FluentAssertions;
using Action = Rgt.Space.Core.Domain.Entities.Permissions.Action; // Alias to avoid collision with System.Action

namespace Rgt.Space.Tests.Unit.Domain.Entities.Permissions;

public class PermissionTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var resourceId = Guid.NewGuid();
        var actionId = Guid.NewGuid();
        var code = "USER_VIEW";
        var description = "View Users";

        // Act
        var permission = Permission.Create(resourceId, actionId, code, description);

        // Assert
        permission.Should().NotBeNull();
        permission.ResourceId.Should().Be(resourceId);
        permission.ActionId.Should().Be(actionId);
        permission.Code.Should().Be(code);
        permission.Description.Should().Be(description);
        permission.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}

public class ResourceTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var moduleId = Guid.NewGuid();
        var name = "Users";
        var code = "USERS";
        var sortOrder = 1;

        // Act
        var resource = Resource.Create(moduleId, name, code, sortOrder);

        // Assert
        resource.Should().NotBeNull();
        resource.ModuleId.Should().Be(moduleId);
        resource.Name.Should().Be(name);
        resource.Code.Should().Be(code);
        resource.SortOrder.Should().Be(sortOrder);
    }
}

public class ModuleTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var name = "Identity";
        var code = "IDENTITY";
        var sortOrder = 10;

        // Act
        var module = Module.Create(name, code, sortOrder);

        // Assert
        module.Should().NotBeNull();
        module.Name.Should().Be(name);
        module.Code.Should().Be(code);
        module.SortOrder.Should().Be(sortOrder);
    }
}

public class ActionTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var name = "View";
        var code = "VIEW";

        // Act
        var action = Action.Create(name, code);

        // Assert
        action.Should().NotBeNull();
        action.Name.Should().Be(name);
        action.Code.Should().Be(code);
    }
}
