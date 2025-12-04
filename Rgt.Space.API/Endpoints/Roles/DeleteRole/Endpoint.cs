using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using DeleteRoleCommand = Rgt.Space.Infrastructure.Commands.Identity.DeleteRole.Command;

namespace Rgt.Space.API.Endpoints.Roles.DeleteRole;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/roles/{roleId}");
        Summary(s =>
        {
            s.Summary = "Delete a role";
            s.Description = "Deletes a role. Cannot delete system roles or roles with assigned users.";
            s.Response(200, "Role deleted successfully");
            s.Response(403, "Cannot delete system role");
            s.Response(404, "Role not found");
            s.Response(409, "Role has assigned users");
        });
        Tags("Role Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        
        var command = new DeleteRoleCommand(roleId);
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
