using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Contracts.Features;
using CreateFeatureCommand = Rgt.Space.Infrastructure.Commands.Features.CreateFeature.Command;

namespace Rgt.Space.API.Endpoints.Features.CreateFeature;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<CreateFeatureRequest>
{
    public override void Configure()
    {
        Post("/api/v1/features");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.GlobalEdit);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Create a new feature flag";
            s.Description = "Creates a new feature with the given code, name, and configuration.";
            s.Response(201, "Feature created successfully");
            s.Response(400, "Validation failure");
            s.Response(409, "Feature code already exists");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CreateFeatureRequest req, CancellationToken ct)
    {
        var command = new CreateFeatureCommand(
            req.Code,
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

        await HttpContext.Response.SendCreatedAtAsync<GetFeatureById.Endpoint>(
            new { featureId = result.Value },
            new { Id = result.Value, req.Code, req.Name },
            cancellation: ct);
    }
}
