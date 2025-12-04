using FastEndpoints;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.API.Endpoints.PortalRouting.GetProjectById;

public sealed class Endpoint(
    IProjectReadDac readDac,
    PortalRoutingMapper mapper) : EndpointWithoutRequest<ProjectResponse>
{
    public override void Configure()
    {
        Get("/api/v1/portal-routing/projects/{id}");
        Summary(s =>
        {
            s.Summary = "Get project by ID";
            s.Description = "Retrieves a single project by its unique identifier";
            s.Response(200, "Project found");
            s.Response(404, "Project not found");
        });
        Tags("Project Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var project = await readDac.GetByIdAsync(id, ct);

        if (project is null)
        {
            await HttpContext.Response.SendAsync<object>(null!, 404, cancellation: ct);
            return;
        }

        var response = mapper.ToResponse(project);
        await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
    }
}
