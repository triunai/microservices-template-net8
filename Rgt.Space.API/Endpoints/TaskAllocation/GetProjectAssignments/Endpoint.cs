using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using TaskAllocationQueries = Rgt.Space.Infrastructure.Queries.TaskAllocation;

namespace Rgt.Space.API.Endpoints.TaskAllocation.GetProjectAssignments;

public class GetProjectAssignmentsRequest
{
    public Guid ProjectId { get; set; }
}

public sealed class Endpoint(IMediator mediator) : Endpoint<GetProjectAssignmentsRequest>
{
    public override void Configure()
    {
        Get("/api/v1/projects/{projectId:guid}/assignments");
        // AllowAnonymous(); // TODO: Add Auth

        Summary(s =>
        {
            s.Summary = "Get project assignments";
            s.Description = "Retrieves the staffing matrix for a project";
            s.Response(200, "Assignments returned successfully");
        });
    }

    public override async Task HandleAsync(GetProjectAssignmentsRequest req, CancellationToken ct)
    {
        var res = await mediator.Send(new TaskAllocationQueries.GetProjectAssignments.Query(req.ProjectId), ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(res.Value, ct);
    }
}
