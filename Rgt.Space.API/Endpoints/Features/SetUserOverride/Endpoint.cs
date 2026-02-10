using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Contracts.Features;
using SetUserOverrideCommand = Rgt.Space.Infrastructure.Commands.Features.SetUserOverride.Command;

namespace Rgt.Space.API.Endpoints.Features.SetUserOverride;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<SetUserOverrideRequest>
{
    public override void Configure()
    {
        Post("/api/v1/users/{userId:guid}/feature-overrides");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.OverrideInsert);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Set user feature override";
            s.Description = "Creates or updates a user-level feature override (FORCE_ON or FORCE_OFF).";
            s.Response(204, "Override set successfully");
            s.Response(400, "Validation failure (invalid override state)");
            s.Response(404, "Feature or user not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(SetUserOverrideRequest req, CancellationToken ct)
    {
        var userId = Route<Guid>("userId");

        var command = new SetUserOverrideCommand(
            userId, req.FeatureCode,
            req.OverrideState, req.Reason,
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
