using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Tests.Unit.Domain.Entities;

public class UserTests
{
    [Fact]
    public void CreateFromSso_ShouldCreateUserWithCorrectProperties()
    {
        // Arrange
        var externalId = "google_12345";
        var email = "john.doe@example.com";
        var displayName = "John Doe";
        var provider = "google";

        // Act
        var user = User.CreateFromSso(externalId, email, displayName, provider);

        // Assert
        user.ExternalId.Should().Be(externalId);
        user.Email.Should().Be(email);
        user.DisplayName.Should().Be(displayName);
        user.SsoProvider.Should().Be(provider);
        user.SsoEmail.Should().Be(email);
        user.IsActive.Should().BeTrue();
        user.SsoLoginEnabled.Should().BeTrue();
        user.LocalLoginEnabled.Should().BeFalse();
        user.Id.Should().NotBeEmpty();
    }

    [Fact]
    public void UpdateLastLogin_ShouldUpdateLastLoginTimestamp()
    {
        // Arrange
        var user = User.CreateFromSso("ext_123", "test@example.com", "Test User", "azuread");
        var beforeUpdate = DateTime.UtcNow;

        // Act
        user.UpdateLastLogin("azuread");

        // Assert
        user.LastLoginAt.Should().NotBeNull();
        user.LastLoginAt.Should().BeOnOrAfter(beforeUpdate);
        user.LastLoginProvider.Should().Be("azuread");
    }

    [Fact]
    public void UpdateFromSso_ShouldUpdateDisplayNameAndEmail()
    {
        // Arrange
        var user = User.CreateFromSso("ext_123", "old@example.com", "Old Name", "google");
        var newDisplayName = "New Name";
        var newEmail = "new@example.com";

        // Act
        user.UpdateFromSso(newDisplayName, newEmail);

        // Assert
        user.DisplayName.Should().Be(newDisplayName);
        user.Email.Should().Be(newEmail);
    }

    [Fact]
    public void Rehydrate_ShouldRecreateUserWithAllProperties()
    {
       // Arrange
        var id = Guid.NewGuid();
        var displayName = "Test User";
        var email = "test@example.com";
        var contactNumber = "+60123456789";
        var isActive = true;
        var localLoginEnabled = false;
        var ssoLoginEnabled = true;
        var ssoProvider = "azuread";
        var externalId = "azure_12345";
        var ssoEmail = "test@example.com";
        var lastLoginAt = DateTime.UtcNow.AddDays(-1);
        var lastLoginProvider = "azuread";
        var createdAt = DateTime.UtcNow.AddMonths(-1);
        Guid? createdBy = Guid.NewGuid();
        var updatedAt = DateTime.UtcNow;
        Guid? updatedBy = Guid.NewGuid();

        // Act
        var user = User.Rehydrate(
            id, displayName, email, contactNumber, isActive,
            localLoginEnabled, null, null,
            ssoLoginEnabled, ssoProvider, externalId, ssoEmail,
            lastLoginAt, lastLoginProvider,
            createdAt, createdBy, updatedAt, updatedBy);

        // Assert
        user.Id.Should().Be(id);
        user.DisplayName.Should().Be(displayName);
        user.Email.Should().Be(email);
        user.ContactNumber.Should().Be(contactNumber);
        user.IsActive.Should().Be(isActive);
        user.LocalLoginEnabled.Should().Be(localLoginEnabled);
        user.SsoLoginEnabled.Should().Be(ssoLoginEnabled);
        user.SsoProvider.Should().Be(ssoProvider);
        user.ExternalId.Should().Be(externalId);
        user.SsoEmail.Should().Be(ssoEmail);
        user.LastLoginAt.Should().Be(lastLoginAt);
        user.LastLoginProvider.Should().Be(lastLoginProvider);
        user.CreatedAt.Should().Be(createdAt);
        user.CreatedBy.Should().Be(createdBy);
        user.UpdatedAt.Should().Be(updatedAt);
        user.UpdatedBy.Should().Be(updatedBy);
    }
}
