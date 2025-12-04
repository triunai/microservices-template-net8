using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Infrastructure.Queries.PortalRouting;
using GetProjectsByClientQuery = Rgt.Space.Infrastructure.Queries.PortalRouting.GetProjectsByClient.GetProjectsByClientQuery;

namespace Rgt.Space.API.Endpoints.PortalRouting.GetProjectsByClient;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/portal-routing/clients/{clientId:guid}/projects");
        // AllowAnonymous(); // TODO: Add proper authorization

        Summary(s =>
        {
            s.Summary = "Get projects by client";
            s.Description = "Retrieves all active projects for a specific client";
            s.Response(200, "List of projects returned successfully");
            s.Response(404, "Client not found");
        });
        Tags("Client Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var clientId = Route<Guid>("clientId");

        var res = await mediator.Send(new GetProjectsByClientQuery(clientId), ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(res.Value, ct);
    }
}
