namespace Rgt.Space.Core.ReadModels;

public sealed record UserReadModel(
    Guid Id,
    string DisplayName,
    string Email,
    string? ContactNumber,
    bool IsActive,
    bool LocalLoginEnabled,
    bool SsoLoginEnabled,
    string? SsoProvider,
    string? ExternalId,
    DateTime? LastLoginAt,
    string? LastLoginProvider,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime UpdatedAt,
    Guid? UpdatedBy
);
