using Rgt.Space.Core.Domain.Primitives;
using Rgt.Space.Core.Utilities; // Assuming Uuid7 is here, checking file location first might be safer but standard pattern suggests Utilities or Primitives

namespace Rgt.Space.Core.Domain.Entities.Identity;

public sealed class User : AuditableEntity
{
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? ContactNumber { get; private set; }
    public bool IsActive { get; private set; }

    // Local Auth
    public bool LocalLoginEnabled { get; private set; }
    public byte[]? PasswordHash { get; private set; }
    public byte[]? PasswordSalt { get; private set; }
    public DateTime? PasswordExpiryAt { get; private set; }
    public string? PasswordResetToken { get; private set; }
    public DateTime? PasswordResetExpiresAt { get; private set; }
    
    // SSO Auth
    public bool SsoLoginEnabled { get; private set; }
    public string? SsoProvider { get; private set; }
    public string? ExternalId { get; private set; }
    public string? SsoEmail { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? LastLoginProvider { get; private set; }

    private User(Guid id) : base(id) { }

    /// <summary>
    /// Creates a new user from SSO provisioning.
    /// </summary>
    public static User CreateFromSso(
        string externalId, 
        string email, 
        string displayName, 
        string provider)
    {
        // Use UUIDv7 for time-ordered IDs
        return new User(Uuid7.NewUuid7())
        {
            ExternalId = externalId,
            Email = email,
            DisplayName = displayName,
            SsoProvider = provider,
            SsoEmail = email,
            IsActive = true,
            SsoLoginEnabled = true,
            LocalLoginEnabled = false, // Default to SSO only for JIT
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Rehydrates a user from the database.
    /// </summary>
    public static User Rehydrate(
        Guid id,
        string displayName,
        string email,
        string? contactNumber,
        bool isActive,
        bool localLoginEnabled,
        byte[]? passwordHash,
        byte[]? passwordSalt,
        DateTime? passwordExpiryAt,
        string? passwordResetToken,
        DateTime? passwordResetExpiresAt,
        bool ssoLoginEnabled,
        string? ssoProvider,
        string? externalId,
        string? ssoEmail,
        DateTime? lastLoginAt,
        string? lastLoginProvider,
        DateTime createdAt,
        Guid? createdBy,
        DateTime updatedAt,
        Guid? updatedBy,
        bool isDeleted,
        DateTime? deletedAt,
        Guid? deletedBy)
    {
        return new User(id)
        {
            DisplayName = displayName,
            Email = email,
            ContactNumber = contactNumber,
            IsActive = isActive,
            LocalLoginEnabled = localLoginEnabled,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            PasswordExpiryAt = passwordExpiryAt,
            PasswordResetToken = passwordResetToken,
            PasswordResetExpiresAt = passwordResetExpiresAt,
            SsoLoginEnabled = ssoLoginEnabled,
            SsoProvider = ssoProvider,
            ExternalId = externalId,
            SsoEmail = ssoEmail,
            LastLoginAt = lastLoginAt,
            LastLoginProvider = lastLoginProvider,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy,
            IsDeleted = isDeleted,
            DeletedAt = deletedAt,
            DeletedBy = deletedBy
        };
    }

    public void UpdateLastLogin(string provider)
    {
        LastLoginAt = DateTime.UtcNow;
        LastLoginProvider = provider;
    }

    public void UpdateFromSso(string displayName, string email)
    {
        DisplayName = displayName;
        // We might not want to update Email if it's the primary key for lookup, 
        // but for SSO sync it's good to keep it fresh.
        if (!string.IsNullOrEmpty(email)) Email = email;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetails(string displayName, string email, string? contactNumber, bool isActive, Guid updatedBy)
    {
        DisplayName = displayName;
        Email = email;
        ContactNumber = contactNumber;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void LinkSso(string provider, string externalId, string ssoEmail)
    {
        SsoProvider = provider;
        ExternalId = externalId;
        SsoEmail = ssoEmail;
        SsoLoginEnabled = true;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Reactivate()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
        IsActive = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
