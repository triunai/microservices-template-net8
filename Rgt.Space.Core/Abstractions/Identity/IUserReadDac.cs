using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.Identity;

public interface IUserReadDac
{
    Task<UserReadModel?> GetByIdAsync(Guid userId, CancellationToken ct);
    Task<UserReadModel?> GetByEmailAsync(string email, CancellationToken ct);
    Task<UserReadModel?> GetByEmailAnyAsync(string email, CancellationToken ct);
    Task<UserReadModel?> GetByExternalIdAsync(string provider, string externalId, CancellationToken ct);
    Task<IReadOnlyList<UserReadModel>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<UserReadModel>> SearchAsync(string searchTerm, CancellationToken ct);
    Task<IReadOnlyList<UserPermissionReadModel>> GetPermissionsAsync(Guid userId, CancellationToken ct);
}
