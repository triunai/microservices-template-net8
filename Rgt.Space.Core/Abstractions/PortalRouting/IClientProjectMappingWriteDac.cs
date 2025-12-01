using System;
using System.Threading;
using System.Threading.Tasks;

namespace Rgt.Space.Core.Abstractions.PortalRouting;

public interface IClientProjectMappingWriteDac
{
    /// <summary>
    /// Creates a new client-project mapping.
    /// </summary>
    Task<Guid> CreateAsync(Guid projectId, string routingUrl, string environment, CancellationToken ct);

    /// <summary>
    /// Updates an existing client-project mapping.
    /// </summary>
    Task UpdateAsync(Guid id, string routingUrl, string environment, string status, CancellationToken ct);

    /// <summary>
    /// Soft deletes a client-project mapping.
    /// </summary>
    Task SoftDeleteAsync(Guid id, CancellationToken ct);
}
