using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.Identity;

public sealed class User : Entity
{
    public string DisplayName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? ContactNumber { get; private set; }
    public bool IsActive { get; private set; }

    // Local Auth
    public bool LocalLoginEnabled { get; private set; }
    public byte[]? PasswordHash { get; private set; }
    public byte[]? PasswordSalt { get; private set; }
    
    // SSO Auth
    public bool SsoLoginEnabled { get; private set; }
    public string? SsoProvider { get; private set; }
    public string? ExternalId { get; private set; }
    public string? SsoEmail { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public string? LastLoginProvider { get; private set; }

    // Audit
    public DateTime CreatedAt { get; private set; }
    public Guid? CreatedBy { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public Guid? UpdatedBy { get; private set; }

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
        return new User(Guid.NewGuid()) // ID will be replaced by DB default (UUID v7) or we generate here? 
                                        // Better to let DB generate or generate v7 here. 
                                        // For now, Guid.NewGuid() is fine as placeholder if we use v7 generator in infra.
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
        bool ssoLoginEnabled,
        string? ssoProvider,
        string? externalId,
        string? ssoEmail,
        DateTime? lastLoginAt,
        string? lastLoginProvider,
        DateTime createdAt,
        Guid? createdBy,
        DateTime updatedAt,
        Guid? updatedBy)
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
            SsoLoginEnabled = ssoLoginEnabled,
            SsoProvider = ssoProvider,
            ExternalId = externalId,
            SsoEmail = ssoEmail,
            LastLoginAt = lastLoginAt,
            LastLoginProvider = lastLoginProvider,
            CreatedAt = createdAt,
            CreatedBy = createdBy,
            UpdatedAt = updatedAt,
            UpdatedBy = updatedBy
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
}
