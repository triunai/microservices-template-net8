using Rgt.Space.Core.Domain.Entities.Identity;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.Identity;

public class UserSessionTests
{
    [Fact]
    public void Create_ShouldInitializeCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var refreshToken = "token_123";
        var expiresAt = DateTime.UtcNow.AddDays(7);
        var ip = "127.0.0.1";
        var deviceInfo = "Test Device";

        // Act
        var session = UserSession.Create(userId, refreshToken, expiresAt, ip, deviceInfo);

        // Assert
        session.Should().NotBeNull();
        session.UserId.Should().Be(userId);
        session.RefreshToken.Should().Be(refreshToken);
        session.ExpiresAt.Should().Be(expiresAt);
        session.CreatedIp.Should().Be(ip);
        session.DeviceInfo.Should().Be(deviceInfo);
        session.IsRevoked.Should().BeFalse();
        session.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Revoke_ShouldMarkAsRevoked()
    {
        // Arrange
        var session = UserSession.Create(Guid.NewGuid(), "token", DateTime.UtcNow.AddDays(1), "ip", "device");
        var replacedBy = "new_token";

        // Act
        session.Revoke(replacedBy);

        // Assert
        session.IsRevoked.Should().BeTrue();
        session.RevokedAt.Should().NotBeNull();
        session.ReplacedBy.Should().Be(replacedBy);
        session.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
