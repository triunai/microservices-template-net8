using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using TaskAllocationCommands = Rgt.Space.Infrastructure.Commands.TaskAllocation;

namespace Rgt.Space.API.Endpoints.TaskAllocation.AssignUser;

public class AssignUserRequest
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string PositionCode { get; set; } = default!;
}

public sealed class Endpoint(IMediator mediator) : Endpoint<AssignUserRequest>
{
    public override void Configure()
    {
        Post("/api/v1/projects/{projectId:guid}/assignments");
        AllowAnonymous(); // TODO: Auth

        Summary(s =>
        {
            s.Summary = "Assign user to project";
            s.Description = "Assigns a user to a specific position on a project";
            s.Response(200, "Assignment successful");
            s.Response(400, "Validation error");
        });
    }

    public override async Task HandleAsync(AssignUserRequest req, CancellationToken ct)
    {
        // TODO: Extract from claims
        Guid? assignedBy = null;

        var cmd = new TaskAllocationCommands.AssignUser.Command(req.ProjectId, req.UserId, req.PositionCode, assignedBy);
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
