using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;
using AssignRoleCommand = Rgt.Space.Infrastructure.Commands.Identity.AssignRoleToUser.Command;

namespace Rgt.Space.API.Endpoints.Identity.AssignRole;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<AssignRoleRequest>
{
    public override void Configure()
    {
        Post("/api/v1/users/{userId}/roles");
        Summary(s =>
        {
            s.Summary = "Assign role to user";
            s.Description = "Assigns a role to a user. Idempotent - if already assigned, returns 200.";
            s.Response(201, "Role assigned successfully");
            s.Response(200, "Role was already assigned");
            s.Response(404, "User or role not found");
        });
        Tags("User Management");
    }

    public override async Task HandleAsync(AssignRoleRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        
        var command = new AssignRoleCommand(
            userId,
            req.RoleId,
            currentUser.Id);

        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        if (result.Value.WasCreated)
        {
            // New assignment created
            await HttpContext.Response.SendAsync(
                new { id = result.Value.UserRoleId, userId, roleId = req.RoleId, assignedAt = DateTime.UtcNow },
                201,
                cancellation: ct);
        }
        else
        {
            // Already assigned
            await Send.OkAsync(new { message = "Role was already assigned" }, ct);
        }
    }
}
