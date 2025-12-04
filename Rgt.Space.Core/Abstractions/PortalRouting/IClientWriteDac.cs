using Rgt.Space.Core.Abstractions.PortalRouting;

namespace Rgt.Space.Core.Abstractions.PortalRouting;

public interface IClientWriteDac
{
    Task CreateAsync(Guid id, string name, string code, string status, Guid createdBy, CancellationToken ct);
    Task UpdateAsync(Guid id, string name, string code, string status, Guid updatedBy, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct);
}
