using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.PortalRouting;

public interface IClientReadDac
{
    Task<ClientReadModel?> GetByIdAsync(Guid clientId, CancellationToken ct);
    Task<ClientReadModel?> GetByCodeAsync(string code, CancellationToken ct);
    Task<IReadOnlyList<ClientReadModel>> GetAllAsync(CancellationToken ct);
}
