using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.ReadModels;
using EvaluateFeatureQuery = Rgt.Space.Infrastructure.Queries.Features.EvaluateFeature.Query;

namespace Rgt.Space.API.Endpoints.Features.EvaluateFeature;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<FeatureDecision>
{
    public override void Configure()
    {
        Get("/api/v1/features/evaluate/{featureCode}");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Evaluate a single feature flag";
            s.Description = "Evaluates a feature for a given user and optional client context. Returns the decision with reason code.";
            s.Response<FeatureDecision>(200, "Feature evaluation result");
            s.Response(400, "Missing or invalid userId");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var featureCode = Route<string>("featureCode");

        if (!Guid.TryParse(HttpContext.Request.Query["userId"], out var userId) || userId == Guid.Empty)
        {
            AddError("userId query parameter is required and must be a valid non-empty GUID.");
            ThrowIfAnyErrors(); // refactor , exceptions are EXPENSIVE should log instead
            return;
        }

        Guid? clientId = Guid.TryParse(HttpContext.Request.Query["clientId"], out var cid)
            ? cid
            : null;

        var result = await mediator.Send(new EvaluateFeatureQuery(featureCode!, userId, clientId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
