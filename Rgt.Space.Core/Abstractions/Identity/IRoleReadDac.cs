using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.Identity;

public interface IRoleReadDac
{
    Task<RoleReadModel?> GetByIdAsync(Guid roleId, CancellationToken ct = default);
    Task<RoleReadModel?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<RoleReadModel>> GetAllAsync(CancellationToken ct = default);
    
    /// <summary>
    /// Gets the count of users assigned to a role.
    /// </summary>
    Task<int> GetUserCountAsync(Guid roleId, CancellationToken ct = default);
    
    /// <summary>
    /// Gets all roles assigned to a user.
    /// </summary>
    Task<IReadOnlyList<UserRoleReadModel>> GetUserRolesAsync(Guid userId, CancellationToken ct = default);
}
