using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using GetFeatureByIdQuery = Rgt.Space.Infrastructure.Queries.Features.GetFeatureById.Query;

namespace Rgt.Space.API.Endpoints.Features.GetFeatureById;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<FeatureReadModel>
{
    public override void Configure()
    {
        Get("/api/v1/features/{featureId:guid}");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.ListView);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Get feature by ID";
            s.Description = "Retrieves a single feature by its unique identifier.";
            s.Response<FeatureReadModel>(200, "Feature details");
            s.Response(404, "Feature not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var featureId = Route<Guid>("featureId");
        var result = await mediator.Send(new GetFeatureByIdQuery(featureId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
