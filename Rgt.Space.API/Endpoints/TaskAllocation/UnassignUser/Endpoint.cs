using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using TaskAllocationCommands = Rgt.Space.Infrastructure.Commands.TaskAllocation;

namespace Rgt.Space.API.Endpoints.TaskAllocation.UnassignUser;

public class UnassignUserRequest
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string PositionCode { get; set; } = default!;
}

public sealed class Endpoint(IMediator mediator) : Endpoint<UnassignUserRequest>
{
    public override void Configure()
    {
        Delete("/api/v1/projects/{projectId:guid}/assignments/{userId:guid}/{positionCode}");
        AllowAnonymous(); // TODO: Auth

        Summary(s =>
        {
            s.Summary = "Unassign user from project";
            s.Description = "Removes a user from a specific position on a project (Soft Delete)";
            s.Response(200, "Unassignment successful");
            s.Response(400, "Validation error");
        });
    }

    public override async Task HandleAsync(UnassignUserRequest req, CancellationToken ct)
    {
        // TODO: Extract from claims
        Guid? unassignedBy = null;

        var cmd = new TaskAllocationCommands.UnassignUser.Command(req.ProjectId, req.UserId, req.PositionCode, unassignedBy);
        var res = await mediator.Send(cmd, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(ct);
    }
}
