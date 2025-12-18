using Rgt.Space.Core.Domain.Entities.PortalRouting;
using Rgt.Space.Core.Constants;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.PortalRouting;

public class ProjectTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var clientId = Guid.NewGuid();
        var name = "Project X";
        var code = "PX";
        var url = "https://example.com";
        var status = StatusConstants.Active;

        // Act
        var project = Project.Create(clientId, name, code, url, status);

        // Assert
        project.Should().NotBeNull();
        project.ClientId.Should().Be(clientId);
        project.Name.Should().Be(name);
        project.Code.Should().Be(code);
        project.ExternalUrl.Should().Be(url);
        project.Status.Should().Be(status);
        project.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
