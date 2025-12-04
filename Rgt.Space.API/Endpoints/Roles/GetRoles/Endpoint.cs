using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Identity;

namespace Rgt.Space.API.Endpoints.Roles.GetRoles;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/roles");
        Summary(s =>
        {
            s.Summary = "Get all roles";
            s.Description = "Returns a list of all roles with user counts. System roles are listed first.";
            s.Response<IReadOnlyList<RoleReadModel>>(200, "List of roles");
        });
        Tags("Role Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllRoles.Query(), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
