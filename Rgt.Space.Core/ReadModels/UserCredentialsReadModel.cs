namespace Rgt.Space.Core.ReadModels;

/// <summary>
/// User credentials model for authentication - includes sensitive fields.
/// NOT for general use - only for login verification.
/// </summary>
public sealed record UserCredentialsReadModel(
    Guid Id,
    string DisplayName,
    string Email,
    bool IsActive,
    bool LocalLoginEnabled,
    byte[]? PasswordHash,
    byte[]? PasswordSalt,
    DateTime? PasswordExpiryAt
);
