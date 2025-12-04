using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Abstractions.PortalRouting;

public interface IProjectWriteDac
{
    Task CreateAsync(Guid id, Guid clientId, string name, string code, string status, string? externalUrl, Guid createdBy, CancellationToken ct);
    Task UpdateAsync(Guid id, string name, string code, string status, string? externalUrl, Guid updatedBy, CancellationToken ct);
    Task DeleteAsync(Guid id, Guid deletedBy, CancellationToken ct);
}
