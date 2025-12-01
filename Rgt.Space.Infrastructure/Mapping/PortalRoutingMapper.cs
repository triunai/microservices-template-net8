using Riok.Mapperly.Abstractions;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Mapping;

/// <summary>
/// Compile-time mapper for Portal Routing domain using Mapperly.
/// Maps ReadModels (DB) → Response DTOs (API).
/// </summary>
[Mapper]
public partial class PortalRoutingMapper
{
    public partial ClientResponse ToResponse(ClientReadModel source);
    public partial ProjectResponse ToResponse(ProjectReadModel source);
    public partial ClientProjectMappingResponse ToResponse(ClientProjectMappingReadModel source);
}
