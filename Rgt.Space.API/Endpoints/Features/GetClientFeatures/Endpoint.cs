using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using GetClientFeaturesQuery = Rgt.Space.Infrastructure.Queries.Features.GetClientFeatures.Query;

namespace Rgt.Space.API.Endpoints.Features.GetClientFeatures;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<ClientFeatureByClientReadModel>>
{
    public override void Configure()
    {
        Get("/api/v1/clients/{clientId:guid}/features");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.ListView);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get features for client";
            s.Description = "Lists all feature subscriptions for a given client, including feature code and name.";
            s.Response<IReadOnlyList<ClientFeatureByClientReadModel>>(200, "List of client features");
            s.Response(404, "Client not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientId = Route<Guid>("clientId");
        var result = await mediator.Send(new GetClientFeaturesQuery(clientId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
