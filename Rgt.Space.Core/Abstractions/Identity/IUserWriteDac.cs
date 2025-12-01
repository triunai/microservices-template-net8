using Rgt.Space.Core.Domain.Entities.Identity;

namespace Rgt.Space.Core.Abstractions.Identity;

public interface IUserWriteDac
{
    Task<Guid> CreateAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task UpdateLastLoginAsync(Guid userId, string provider, CancellationToken ct = default);
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
