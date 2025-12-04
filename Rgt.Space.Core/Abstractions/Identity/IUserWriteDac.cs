using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Core.Abstractions.Identity;

public interface IUserWriteDac
{
    Task<Guid> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(Guid userId, Guid deletedBy, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, string provider, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<bool> GrantPermissionAsync(Guid userId, string module, string subModule, bool canView, bool canInsert, bool canEdit, bool canDelete, Guid grantedBy, CancellationToken ct = default);
    Task<bool> RevokePermissionAsync(Guid userId, string module, string subModule, Guid revokedBy, CancellationToken ct = default);
    
    /// <summary>
    /// Soft-deletes all project assignments for a user.
    /// Returns the count of assignments that were deleted.
    /// </summary>
    Task<int> DeleteUserAssignmentsAsync(Guid userId, Guid deletedBy, CancellationToken ct = default);
}
