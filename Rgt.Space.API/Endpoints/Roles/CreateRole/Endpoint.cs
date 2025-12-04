using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Core.ReadModels;
using CreateRoleCommand = Rgt.Space.Infrastructure.Commands.Identity.CreateRole.Command;

namespace Rgt.Space.API.Endpoints.Roles.CreateRole;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<CreateRoleRequest>
{
    public override void Configure()
    {
        Post("/api/v1/roles");
        Summary(s =>
        {
            s.Summary = "Create a new role";
            s.Description = "Creates a new role. Code must be unique and uppercase.";
            s.Response<RoleReadModel>(201, "Role created successfully");
            s.Response(400, "Validation failure");
            s.Response(409, "Role code already exists");
        });
        Tags("Role Management");
    }

    public override async Task HandleAsync(CreateRoleRequest req, CancellationToken ct)
    {
        var command = new CreateRoleCommand(
            req.Name,
            req.Code,
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

        // Build response (simplified - just return the ID and basic info)
        var response = new RoleReadModel(
            result.Value,
            req.Name,
            req.Code,
            req.Description,
            false, // isSystem - user-created roles are never system
            req.IsActive,
            0, // userCount - new role has no users
            DateTime.UtcNow,
            currentUser.Id,
            DateTime.UtcNow,
            currentUser.Id);

        await HttpContext.Response.SendCreatedAtAsync<GetRole.Endpoint>(
            new { roleId = result.Value },
            response,
            cancellation: ct);
    }
}
