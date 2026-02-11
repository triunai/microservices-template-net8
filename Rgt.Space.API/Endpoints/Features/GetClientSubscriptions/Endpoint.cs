using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using GetClientSubscriptionsQuery = Rgt.Space.Infrastructure.Queries.Features.GetClientSubscriptions.Query;

namespace Rgt.Space.API.Endpoints.Features.GetClientSubscriptions;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<ClientFeatureDetailReadModel>>
{
    public override void Configure()
    {
        Get("/api/v1/features/{featureId:guid}/clients");
        Permissions(FeatureFlagConstants.Permissions.ListView);
        Summary(s =>
        {
            s.Summary = "Get client subscriptions for feature";
            s.Description = "Lists all client subscriptions for a given feature, including client name and code.";
            s.Response<IReadOnlyList<ClientFeatureDetailReadModel>>(200, "List of client subscriptions");
            s.Response(404, "Feature not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var featureId = Route<Guid>("featureId");
        var result = await mediator.Send(new GetClientSubscriptionsQuery(featureId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
