using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Infrastructure.Queries.PortalRouting;
using GetAllClientsQuery = Rgt.Space.Infrastructure.Queries.PortalRouting.GetAllClients.GetAllClientsQuery;

namespace Rgt.Space.API.Endpoints.PortalRouting.GetAllClients;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/portal-routing/clients");
        // AllowAnonymous(); // TODO: Add proper authorization

        Summary(s =>
        {
            s.Summary = "Get all clients";
            s.Description = "Retrieves all active clients for portal navigation";
            s.Response(200, "List of clients returned successfully");
        });
        Tags("Client Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var res = await mediator.Send(new GetAllClientsQuery(), ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(res.Value, ct);
    }
}
