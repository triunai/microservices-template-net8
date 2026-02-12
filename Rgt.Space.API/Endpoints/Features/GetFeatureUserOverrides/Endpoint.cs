using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using GetFeatureUserOverridesQuery = Rgt.Space.Infrastructure.Queries.Features.GetFeatureUserOverrides.Query;

namespace Rgt.Space.API.Endpoints.Features.GetFeatureUserOverrides;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<UserOverrideDetailReadModel>>
{
    public override void Configure()
    {
        Get("/api/v1/features/{featureId:guid}/user-overrides");
        Permissions(FeatureFlagConstants.Permissions.ListView);
        Summary(s =>
        {
            s.Summary = "Get user overrides for feature";
            s.Description = "Lists all user overrides for a given feature, including user display name and email.";
            s.Response<IReadOnlyList<UserOverrideDetailReadModel>>(200, "List of user overrides");
            s.Response(404, "Feature not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var featureId = Route<Guid>("featureId");
        var result = await mediator.Send(new GetFeatureUserOverridesQuery(featureId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
