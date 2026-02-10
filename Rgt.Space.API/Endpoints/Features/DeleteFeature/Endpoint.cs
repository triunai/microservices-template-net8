using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Infrastructure.Commands.Features;

namespace Rgt.Space.API.Endpoints.Features.DeleteFeature;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/features/{featureId:guid}");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.GlobalEdit);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Soft-delete a feature flag";
            s.Description = "Marks a feature as deleted. Soft-deleted features are treated as not found at runtime.";
            s.Response(204, "Feature deleted successfully");
            s.Response(404, "Feature not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var featureId = Route<Guid>("featureId");

        var command = new SoftDeleteFeature.Command(featureId, currentUser.Id);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        HttpContext.Response.StatusCode = 204;
    }
}
