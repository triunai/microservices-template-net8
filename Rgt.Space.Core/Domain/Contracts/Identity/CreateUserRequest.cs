namespace Rgt.Space.Core.Domain.Contracts.Identity;

/// <summary>
/// Request DTO for creating a new user.
/// </summary>
public sealed record CreateUserRequest(
    string DisplayName,
    string Email,
    string? ContactNumber,
    bool LocalLoginEnabled,
    string? Password,
    List<Guid>? RoleIds
);
