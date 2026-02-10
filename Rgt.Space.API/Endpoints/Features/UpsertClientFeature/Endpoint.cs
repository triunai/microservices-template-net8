using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Contracts.Features;
using UpsertClientFeatureCommand = Rgt.Space.Infrastructure.Commands.Features.UpsertClientFeature.Command;

namespace Rgt.Space.API.Endpoints.Features.UpsertClientFeature;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<UpsertClientFeatureRequest>
{
    public override void Configure()
    {
        Put("/api/v1/clients/{clientId:guid}/features/{featureId:guid}");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.ClientEdit);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Upsert client feature subscription";
            s.Description = "Sets the client's subscription state for a feature. Creates if not exists, updates if exists.";
            s.Response(204, "Client feature updated successfully");
            s.Response(404, "Feature or client not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(UpsertClientFeatureRequest req, CancellationToken ct)
    {
        var clientId = Route<Guid>("clientId");
        var featureId = Route<Guid>("featureId");

        var command = new UpsertClientFeatureCommand(
            clientId, featureId,
            req.IsEnabled, currentUser.Id);

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
