using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.ReadModels;
using GetUserRolesQuery = Rgt.Space.Infrastructure.Queries.Identity.GetUserRoles;

namespace Rgt.Space.API.Endpoints.Identity.GetUserRoles;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/users/{userId}/roles");
        Summary(s =>
        {
            s.Summary = "Get user's assigned roles";
            s.Description = "Returns a list of roles assigned to a user.";
            s.Response<IReadOnlyList<UserRoleReadModel>>(200, "List of assigned roles");
            s.Response(404, "User not found");
        });
        Tags("User Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await mediator.Send(new GetUserRolesQuery.Query(userId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}

