using Rgt.Space.Core.Domain.Entities.PortalRouting;
using Rgt.Space.Core.Constants;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.PortalRouting;

public class ClientProjectMappingTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var projectId = Guid.NewGuid();
        var routingUrl = "https://app.example.com";
        var env = "Production";

        // Act
        var mapping = ClientProjectMapping.Create(projectId, routingUrl, env);

        // Assert
        mapping.Should().NotBeNull();
        mapping.ProjectId.Should().Be(projectId);
        mapping.RoutingUrl.Should().Be(routingUrl);
        mapping.Environment.Should().Be(env);
        mapping.Status.Should().Be(StatusConstants.Active);
    }

    [Fact]
    public void UpdateStatus_ShouldUpdateStatusAndTimestamp()
    {
        // Arrange
        var mapping = ClientProjectMapping.Create(Guid.NewGuid(), "url", "env");
        var newStatus = StatusConstants.Inactive;
        var before = DateTime.UtcNow;

        // Act
        mapping.UpdateStatus(newStatus);

        // Assert
        mapping.Status.Should().Be(newStatus);
        mapping.UpdatedAt.Should().BeAfter(before);
    }
}
