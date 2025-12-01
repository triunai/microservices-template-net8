using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.PortalRouting;

public interface IClientProjectMappingReadDac
{
    Task<ClientProjectMappingReadModel?> GetByIdAsync(Guid mappingId, CancellationToken ct);
    Task<ClientProjectMappingReadModel?> GetByRoutingUrlAsync(string routingUrl, CancellationToken ct);
    Task<IReadOnlyList<ClientProjectMappingReadModel>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<ClientProjectMappingReadModel>> GetByProjectIdAsync(Guid projectId, CancellationToken ct);
}
