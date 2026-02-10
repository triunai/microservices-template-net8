using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using BulkEvalQuery = Rgt.Space.Infrastructure.Queries.Features.BulkEvaluateFeatures.Query;
using BulkEvalResponse = Rgt.Space.Infrastructure.Queries.Features.BulkEvaluateFeatures.Response;

namespace Rgt.Space.API.Endpoints.Features.BulkEvaluateFeatures;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<BulkEvalResponse>
{
    public override void Configure()
    {
        Get("/api/v1/features/evaluate");
        // TODO: Restore auth after Swagger testing
        // Permissions(FeatureFlagConstants.Permissions.EvaluateView);
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "Evaluate all feature flags";
            s.Description = "Evaluates all features for a given user and optional client context. Called on frontend login to populate flag cache.";
            s.Response<BulkEvalResponse>(200, "Bulk evaluation result");
            s.Response(400, "Missing or invalid userId");
        });
        Tags("Feature Flags");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(HttpContext.Request.Query["userId"], out var userId) || userId == Guid.Empty)
        {
            AddError("userId query parameter is required and must be a valid non-empty GUID.");
            ThrowIfAnyErrors(); // refactor , exceptions are EXPENSIVE should log instead
            return;
        }

        Guid? clientId = Guid.TryParse(HttpContext.Request.Query["clientId"], out var cid)
            ? cid
            : null;

        var result = await mediator.Send(new BulkEvalQuery(userId, clientId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(result.Value, 200, cancellation: ct);
    }
}
