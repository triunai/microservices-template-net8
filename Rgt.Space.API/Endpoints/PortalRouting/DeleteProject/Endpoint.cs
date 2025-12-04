using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using DeleteProjectCommand = Rgt.Space.Infrastructure.Commands.PortalRouting.DeleteProject.Command;

namespace Rgt.Space.API.Endpoints.PortalRouting.DeleteProject;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/portal-routing/projects/{id}");
        Summary(s =>
        {
            s.Summary = "Delete a project";
            s.Description = "Soft deletes a project and cascades to its mappings and assignments.";
            s.Response(204, "Project deleted successfully");
            s.Response(404, "Project not found");
        });
        Tags("Project Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var command = new DeleteProjectCommand(id);
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
