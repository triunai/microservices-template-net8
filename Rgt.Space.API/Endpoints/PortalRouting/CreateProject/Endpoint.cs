using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using CreateProjectCommand = Rgt.Space.Infrastructure.Commands.PortalRouting.CreateProject.Command;

namespace Rgt.Space.API.Endpoints.PortalRouting.CreateProject;

public sealed class Endpoint(IMediator mediator) : Endpoint<CreateProjectRequest>
{
    public override void Configure()
    {
        Post("/api/v1/portal-routing/projects");
        Summary(s =>
        {
            s.Summary = "Create a new project";
            s.Description = "Creates a new project under a client";
            s.Response(201, "Project created successfully");
            s.Response(400, "Validation failure");
            s.Response(404, "Client not found");
            s.Response(409, "Project code already exists for this client");
        });
        Tags("Project Management");
    }

    public override async Task HandleAsync(CreateProjectRequest req, CancellationToken ct)
    {
        var command = new CreateProjectCommand(req.ClientId, req.Name, req.Code, req.Status, req.ExternalUrl);
        var res = await mediator.Send(command, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendCreatedAtAsync<GetProjectById.Endpoint>(
            new { Id = res.Value },
            new { Id = res.Value },
            cancellation: ct);
    }
}
