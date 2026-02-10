using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using GetUserOverridesQuery = Rgt.Space.Infrastructure.Queries.Features.GetUserOverrides.Query;

namespace Rgt.Space.API.Endpoints.Features.GetUserOverrides;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<UserOverrideByUserReadModel>>
{
    public override void Configure()
    {
        Get("/api/v1/users/{userId:guid}/feature-overrides");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.ListView);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get feature overrides for user";
            s.Description = "Lists all feature overrides for a given user, including feature code and name.";
            s.Response<IReadOnlyList<UserOverrideByUserReadModel>>(200, "List of feature overrides");
            s.Response(404, "User not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await mediator.Send(new GetUserOverridesQuery(userId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
