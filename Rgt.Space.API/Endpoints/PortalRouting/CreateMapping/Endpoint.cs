using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Commands.PortalRouting;

namespace Rgt.Space.API.Endpoints.PortalRouting.CreateMapping;

public sealed class Endpoint(IMediator mediator, Rgt.Space.Core.Abstractions.Identity.ICurrentUser currentUser) : Endpoint<CreateMappingRequest>
{
    public override void Configure()
    {
        Post("/api/v1/portal-routing/mappings");
        // AllowAnonymous(); // TODO: Remove in Phase 2

        Summary(s =>
        {
            s.Summary = "Create a new client-project mapping";
            s.Description = "Creates a new routing URL mapping for a project in a specific environment";
            s.Response(201, "Mapping created successfully");
            s.Response(400, "Validation failure or invalid input");
            s.Response(404, "Project not found");
        });
        Tags("Portal Routing");
    }

    public override async Task HandleAsync(CreateMappingRequest req, CancellationToken ct)
    {
        var createdBy = currentUser.Id;

        var command = new Rgt.Space.Infrastructure.Commands.PortalRouting.CreateMapping.CreateMappingCommand(
            req.ProjectId,
            req.RoutingUrl,
            req.Environment,
            createdBy
        );

        var res = await mediator.Send(command, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendCreatedAtAsync<Rgt.Space.API.Endpoints.PortalRouting.GetAllMappings.Endpoint>(
            new { }, 
            new { Id = res.Value }, 
            cancellation: ct);
    }
}
