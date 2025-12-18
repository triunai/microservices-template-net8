using Rgt.Space.Core.Domain.Entities.PortalRouting;
using Rgt.Space.Core.Constants;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.PortalRouting;

public class ClientTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var name = "Client A";
        var code = "CA";
        var status = StatusConstants.Active;

        // Act
        var client = Client.Create(name, code, status);

        // Assert
        client.Should().NotBeNull();
        client.Name.Should().Be(name);
        client.Code.Should().Be(code);
        client.Status.Should().Be(status);
        client.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
