namespace Rgt.Space.Core.Abstractions.Identity;

public interface IRoleWriteDac
{
    Task<Guid> CreateAsync(Guid id, string name, string code, string? description, bool isActive, Guid createdBy, CancellationToken ct = default);
    Task UpdateAsync(Guid id, string name, string? description, bool isActive, Guid updatedBy, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    
    /// <summary>
    /// Assigns a role to a user.
    /// Returns the user_roles.id if created, or null if already exists (idempotent).
    /// </summary>
    Task<Guid?> AssignRoleToUserAsync(Guid userId, Guid roleId, Guid assignedBy, CancellationToken ct = default);
    
    /// <summary>
    /// Removes a role assignment from a user.
    /// Returns true if deleted, false if not found.
    /// </summary>
    Task<bool> UnassignRoleFromUserAsync(Guid userId, Guid roleId, CancellationToken ct = default);
}
