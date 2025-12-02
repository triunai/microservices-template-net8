using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using TaskAllocationCommands = Rgt.Space.Infrastructure.Commands.TaskAllocation;

namespace Rgt.Space.API.Endpoints.TaskAllocation.UpdateAssignment;

public class UpdateAssignmentRequest
{
    public Guid ProjectId { get; set; }
    public Guid UserId { get; set; }
    public string OldPositionCode { get; set; } = default!;
    public string NewPositionCode { get; set; } = default!;
}

public sealed class Endpoint(IMediator mediator, Rgt.Space.Core.Abstractions.Identity.ICurrentUser currentUser) : Endpoint<UpdateAssignmentRequest>
{
    public override void Configure()
    {
        Put("/api/v1/projects/{projectId:guid}/assignments");
        // AllowAnonymous(); // TODO: Remove in Phase 2

        Summary(s =>
        {
            s.Summary = "Update assignment (Change Role)";
            s.Description = "Updates a user's assignment by moving them from an old position to a new position.";
            s.Response(200, "Update successful");
            s.Response(400, "Validation error");
            s.Response(404, "Assignment not found");
        });
    }

    public override async Task HandleAsync(UpdateAssignmentRequest req, CancellationToken ct)
    {
        var updatedBy = currentUser.Id;

        var cmd = new TaskAllocationCommands.UpdateAssignment.Command(
            req.ProjectId, 
            req.UserId, 
            req.OldPositionCode, 
            req.NewPositionCode, 
            updatedBy);

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
