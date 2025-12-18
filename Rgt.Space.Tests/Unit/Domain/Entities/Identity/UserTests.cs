using Rgt.Space.Core.Domain.Entities.Identity;
using FluentAssertions;

namespace Rgt.Space.Tests.Unit.Domain.Entities.Identity;

public class UserTests
{
    [Fact]
    public void CreateFromSso_ShouldInitializeCorrectly()
    {
        // Arrange
        var externalId = "ext_123";
        var email = "test@example.com";
        var displayName = "Test User";
        var provider = "google";

        // Act
        var user = User.CreateFromSso(externalId, email, displayName, provider);

        // Assert
        user.Should().NotBeNull();
        user.Id.Should().NotBeEmpty();
        user.ExternalId.Should().Be(externalId);
        user.Email.Should().Be(email);
        user.DisplayName.Should().Be(displayName);
        user.SsoProvider.Should().Be(provider);
        user.IsActive.Should().BeTrue();
        user.SsoLoginEnabled.Should().BeTrue();
        user.LocalLoginEnabled.Should().BeFalse();
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CreateManual_ShouldInitializeCorrectly()
    {
        // Arrange
        var displayName = "Admin User";
        var email = "admin@example.com";
        var createdBy = Guid.NewGuid();

        // Act
        var user = User.CreateManual(displayName, email, null, true, null, null, createdBy);

        // Assert
        user.Should().NotBeNull();
        user.Email.Should().Be(email);
        user.CreatedBy.Should().Be(createdBy);
        user.LocalLoginEnabled.Should().BeTrue();
        user.SsoLoginEnabled.Should().BeFalse();
    }

    [Fact]
    public void UpdateLastLogin_ShouldUpdateTimestampAndProvider()
    {
        // Arrange
        var user = User.CreateFromSso("123", "a@b.com", "A", "p");
        var provider = "new_provider";
        var before = DateTime.UtcNow;

        // Act
        user.UpdateLastLogin(provider);

        // Assert
        user.LastLoginAt.Should().BeAfter(before);
        user.LastLoginProvider.Should().Be(provider);
    }

    [Fact]
    public void SoftDelete_ShouldMarkAsDeleted()
    {
        // Arrange
        var user = User.CreateFromSso("123", "a@b.com", "A", "p");
        var deletedBy = Guid.NewGuid();

        // Act
        user.SoftDelete(deletedBy);

        // Assert
        user.IsDeleted.Should().BeTrue();
        user.DeletedAt.Should().NotBeNull();
        user.DeletedBy.Should().Be(deletedBy);
        user.IsActive.Should().BeFalse();
    }
}
