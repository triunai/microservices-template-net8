using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using UnassignRoleCommand = Rgt.Space.Infrastructure.Commands.Identity.UnassignRoleFromUser.Command;

namespace Rgt.Space.API.Endpoints.Identity.UnassignRole;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/users/{userId}/roles/{roleId}");
        Summary(s =>
        {
            s.Summary = "Unassign role from user";
            s.Description = "Removes a role assignment from a user. Idempotent - if not found, still returns 200.";
            s.Response(200, "Role unassigned successfully");
            s.Response(404, "User not found");
        });
        Tags("User Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var roleId = Route<Guid>("roleId");
        
        var command = new UnassignRoleCommand(userId, roleId);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await Send.OkAsync(new { deleted = true }, ct);
    }
}
