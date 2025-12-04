using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using UpdateProjectCommand = Rgt.Space.Infrastructure.Commands.PortalRouting.UpdateProject.Command;

namespace Rgt.Space.API.Endpoints.PortalRouting.UpdateProject;

public sealed class Endpoint(IMediator mediator) : Endpoint<UpdateProjectRequest>
{
    public override void Configure()
    {
        Put("/api/v1/portal-routing/projects/{id}");
        Summary(s =>
        {
            s.Summary = "Update a project";
            s.Description = "Updates an existing project";
            s.Response(200, "Project updated successfully");
            s.Response(400, "Validation failure");
            s.Response(404, "Project not found");
            s.Response(409, "Project code already exists for this client");
        });
        Tags("Project Management");
    }

    public override async Task HandleAsync(UpdateProjectRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var command = new UpdateProjectCommand(id, req.Name, req.Code, req.Status, req.ExternalUrl);
        var res = await mediator.Send(command, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync<object>(null!, 204, cancellation: ct);
    }
}
