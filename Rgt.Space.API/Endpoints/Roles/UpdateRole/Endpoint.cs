using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;
using UpdateRoleCommand = Rgt.Space.Infrastructure.Commands.Identity.UpdateRole.Command;

namespace Rgt.Space.API.Endpoints.Roles.UpdateRole;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<UpdateRoleRequest>
{
    public override void Configure()
    {
        Put("/api/v1/roles/{roleId}");
        Summary(s =>
        {
            s.Summary = "Update a role";
            s.Description = "Updates a role's name, description, and active status. Code cannot be changed.";
            s.Response(204, "Role updated successfully");
            s.Response(400, "Validation failure");
            s.Response(403, "Cannot modify system role");
            s.Response(404, "Role not found");
        });
        Tags("Role Management");
    }

    public override async Task HandleAsync(UpdateRoleRequest req, CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        
        var command = new UpdateRoleCommand(
            roleId,
            req.Name,
            req.Description,
            req.IsActive,
            currentUser.Id);

        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync<object>(null!, 204, cancellation: ct);
    }
}

