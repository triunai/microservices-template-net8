namespace Rgt.Space.Core.Abstractions.TaskAllocation;

public interface ITaskAllocationWriteDac
{
    /// <summary>
    /// Assigns a user to a position on a project.
    /// Returns true if successful, false if already assigned (idempotent).
    /// </summary>
    Task<bool> AssignUserAsync(Guid projectId, Guid userId, string positionCode, Guid? assignedBy, CancellationToken ct);

    /// <summary>
    /// Unassigns a user from a position (Soft Delete).
    /// Returns true if a record was removed, false if not found.
    /// </summary>
    Task<bool> UnassignUserAsync(Guid projectId, Guid userId, string positionCode, Guid? unassignedBy, CancellationToken ct);
    /// <summary>
    /// Updates an assignment by soft-deleting the old one and creating a new one.
    /// Returns true if successful.
    /// </summary>
    Task<bool> UpdateAssignmentAsync(Guid projectId, Guid userId, string oldPositionCode, string newPositionCode, Guid? updatedBy, CancellationToken ct);
}
