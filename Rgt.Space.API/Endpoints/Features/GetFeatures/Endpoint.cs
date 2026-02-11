using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Queries.Features;

namespace Rgt.Space.API.Endpoints.Features.GetFeatures;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<IReadOnlyList<FeatureReadModel>>
{
    public override void Configure()
    {
        Get("/api/v1/features");
        Permissions(FeatureFlagConstants.Permissions.ListView);
        Summary(s =>
        {
            s.Summary = "Get all features";
            s.Description = "Returns a list of all non-deleted features for admin management.";
            s.Response<IReadOnlyList<FeatureReadModel>>(200, "List of features");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAllFeatures.Query(), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
