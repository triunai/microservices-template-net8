using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Commands.PortalRouting;

namespace Rgt.Space.API.Endpoints.PortalRouting.UpdateMapping;

public sealed class Endpoint(IMediator mediator, Rgt.Space.Core.Abstractions.Identity.ICurrentUser currentUser) : Endpoint<UpdateMappingRequest>
{
    public override void Configure()
    {
        Put("/api/v1/portal-routing/mappings/{id:guid}");
        // AllowAnonymous(); // TODO: Remove in Phase 2

        Summary(s =>
        {
            s.Summary = "Update an existing client-project mapping";
            s.Description = "Updates the routing URL, environment, or active status of a mapping";
            s.Response(204, "Mapping updated successfully");
            s.Response(400, "Validation failure or invalid input");
            s.Response(404, "Mapping not found");
        });
    }

    public override async Task HandleAsync(UpdateMappingRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var updatedBy = currentUser.Id;
        
        var command = new Rgt.Space.Infrastructure.Commands.PortalRouting.UpdateMapping.UpdateMappingCommand(
            id,
            req.RoutingUrl,
            req.Environment,
            req.Status,
            updatedBy
        );

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
