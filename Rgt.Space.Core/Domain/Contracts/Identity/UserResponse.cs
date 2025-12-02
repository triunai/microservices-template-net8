namespace Rgt.Space.Core.Domain.Contracts.Identity;

public record UserResponse(
    Guid Id,
    string DisplayName,
    string Email,
    string? ContactNumber,
    bool IsActive,
    DateTime CreatedAt,
    Guid? CreatedBy,
    DateTime UpdatedAt,
    Guid? UpdatedBy);
