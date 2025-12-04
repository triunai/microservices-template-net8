using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Identity;

namespace Rgt.Space.API.Endpoints.Roles.GetRole;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/roles/{roleId}");
        Summary(s =>
        {
            s.Summary = "Get role by ID";
            s.Description = "Returns a single role with user count.";
            s.Response<RoleReadModel>(200, "Role details");
            s.Response(404, "Role not found");
        });
        Tags("Role Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var roleId = Route<Guid>("roleId");
        var result = await mediator.Send(new GetRoleById.Query(roleId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
