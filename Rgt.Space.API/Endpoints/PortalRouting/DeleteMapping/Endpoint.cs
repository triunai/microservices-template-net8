using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Infrastructure.Commands.PortalRouting;

namespace Rgt.Space.API.Endpoints.PortalRouting.DeleteMapping;

public sealed class Endpoint(IMediator mediator, Rgt.Space.Core.Abstractions.Identity.ICurrentUser currentUser) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/portal-routing/mappings/{id:guid}");
        // AllowAnonymous(); // TODO: Remove in Phase 2

        Summary(s =>
        {
            s.Summary = "Soft delete a client-project mapping";
            s.Description = "Marks a routing URL mapping as deleted without removing it from the database";
            s.Response(204, "Mapping deleted successfully");
            s.Response(404, "Mapping not found");
        });
        Tags("Portal Routing");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var deletedBy = currentUser.Id;
        
        var command = new Rgt.Space.Infrastructure.Commands.PortalRouting.DeleteMapping.DeleteMappingCommand(id, deletedBy);

        var res = await mediator.Send(command, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendNoContentAsync(cancellation: ct);
    }
}
