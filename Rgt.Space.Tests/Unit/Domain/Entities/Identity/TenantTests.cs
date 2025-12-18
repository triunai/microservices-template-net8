using Rgt.Space.Core.Domain.Entities.Identity;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.Identity;

public class TenantTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var name = "Test Tenant";
        var code = "test";
        var connectionString = "Host=localhost;Database=test";

        // Act
        var tenant = Tenant.Create(name, code, connectionString);

        // Assert
        tenant.Should().NotBeNull();
        tenant.Id.Should().NotBeEmpty();
        tenant.Name.Should().Be(name);
        tenant.Code.Should().Be(code);
        tenant.ConnectionString.Should().Be(connectionString);
        tenant.Status.Should().Be("Active");
        tenant.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
