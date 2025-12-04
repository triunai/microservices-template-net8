using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Infrastructure.Queries.PortalRouting;
using GetAllMappingsQuery = Rgt.Space.Infrastructure.Queries.PortalRouting.GetAllMappings.GetAllMappingsQuery;

namespace Rgt.Space.API.Endpoints.PortalRouting.GetAllMappings;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/portal-routing/mappings");
        // AllowAnonymous(); // TODO: Add proper authorization

        Summary(s =>
        {
            s.Summary = "Get all client-project mappings";
            s.Description = "Retrieves all routing URL mappings for admin console view";
            s.Response(200, "List of mappings returned successfully");
        });
        Tags("Portal Routing");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var res = await mediator.Send(new GetAllMappingsQuery(), ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(res.Value, ct);
    }
}
