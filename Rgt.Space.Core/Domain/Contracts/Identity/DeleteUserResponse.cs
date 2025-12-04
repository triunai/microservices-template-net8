namespace Rgt.Space.Core.Domain.Contracts.Identity;

/// <summary>
/// Response DTO for Delete User endpoint.
/// Includes count of cascade-deleted assignments for frontend warning.
/// </summary>
public sealed record DeleteUserResponse(
    bool Deleted,
    int AssignmentsRemoved
);
