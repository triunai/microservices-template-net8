using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using ClearUserOverrideCommand = Rgt.Space.Infrastructure.Commands.Features.ClearUserOverride.Command;

namespace Rgt.Space.API.Endpoints.Features.ClearUserOverride;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/users/{userId:guid}/feature-overrides/{featureCode}");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.OverrideDelete);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Clear user feature override";
            s.Description = "Removes a user-level feature override (hard delete). User reverts to client/global decision.";
            s.Response(204, "Override cleared successfully");
            s.Response(404, "Feature not found");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var featureCode = Route<string>("featureCode");

        var command = new ClearUserOverrideCommand(userId, featureCode!);
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
