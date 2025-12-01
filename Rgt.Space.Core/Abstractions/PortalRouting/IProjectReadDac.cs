using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.PortalRouting;

public interface IProjectReadDac
{
    Task<ProjectReadModel?> GetByIdAsync(Guid projectId, CancellationToken ct);
    Task<IReadOnlyList<ProjectReadModel>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<ProjectReadModel>> GetByClientIdAsync(Guid clientId, CancellationToken ct);
}
