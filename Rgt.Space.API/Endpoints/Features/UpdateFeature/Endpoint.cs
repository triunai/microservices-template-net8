using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Contracts.Features;
using UpdateFeatureCommand = Rgt.Space.Infrastructure.Commands.Features.UpdateFeature.Command;

namespace Rgt.Space.API.Endpoints.Features.UpdateFeature;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<UpdateFeatureRequest>
{
    public override void Configure()
    {
        Put("/api/v1/features/{featureId:guid}");
        Permissions(FeatureFlagConstants.Permissions.GlobalEdit);
        Summary(s =>
        {
            s.Summary = "Update a feature flag";
            s.Description = "Updates feature properties. CRITICAL #8: Full PUT with explicit values, not toggle.";
            s.Response(204, "Feature updated successfully");
            s.Response(400, "Validation failure");
            s.Response(404, "Feature not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(UpdateFeatureRequest req, CancellationToken ct)
    {
        var featureId = Route<Guid>("featureId");

        var command = new UpdateFeatureCommand(
            featureId,
            req.Name,
            req.Description,
            req.IsActive,
            req.RequiresClient,
            currentUser.Id);

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
