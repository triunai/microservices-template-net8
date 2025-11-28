using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Core.Abstractions.Identity;

public interface IUserReadDac
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByExternalIdAsync(string provider, string externalId, CancellationToken ct = default);
}
