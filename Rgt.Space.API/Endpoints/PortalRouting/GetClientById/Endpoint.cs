using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.ReadModels;
using GetClientByIdQuery = Rgt.Space.Infrastructure.Queries.PortalRouting.GetClientById.Query;

namespace Rgt.Space.API.Endpoints.PortalRouting.GetClientById;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest<ClientReadModel>
{
    public override void Configure()
    {
        Get("/api/v1/portal-routing/clients/{id}");
        Summary(s =>
        {
            s.Summary = "Get client by ID";
            s.Description = "Retrieves a single client by its unique identifier";
            s.Response(200, "Client details returned successfully");
            s.Response(404, "Client not found");
        });
        Tags("Client Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var res = await mediator.Send(new GetClientByIdQuery(id), ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync(res.Value, 200, cancellation: ct);
    }
}
