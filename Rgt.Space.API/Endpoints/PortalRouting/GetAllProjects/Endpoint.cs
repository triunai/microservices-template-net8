using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using GetAllProjectsQuery = Rgt.Space.Infrastructure.Queries.PortalRouting.GetAllProjects.Query;

namespace Rgt.Space.API.Endpoints.PortalRouting.GetAllProjects;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<ProjectResponse>>
{
    public override void Configure()
    {
        Get("/api/v1/portal-routing/projects");
        Summary(s =>
        {
            s.Summary = "Get all projects";
            s.Description = "Retrieves all active projects across all clients";
            s.Response(200, "List of projects returned successfully");
        });
        Tags("Project Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var res = await mediator.Send(new GetAllProjectsQuery(), ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(res.Value, 200, cancellation: ct);
    }
}
