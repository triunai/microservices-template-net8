using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using UpdateClientCommand = Rgt.Space.Infrastructure.Commands.PortalRouting.UpdateClient.Command;

namespace Rgt.Space.API.Endpoints.PortalRouting.UpdateClient;

public sealed class Endpoint(IMediator mediator) : Endpoint<UpdateClientRequest>
{
    public override void Configure()
    {
        Put("/api/v1/portal-routing/clients/{id}");
        Summary(s =>
        {
            s.Summary = "Update a client";
            s.Description = "Updates an existing client organization";
            s.Response(200, "Client updated successfully");
            s.Response(404, "Client not found");
            s.Response(409, "Client code already exists");
        });
        Tags("Client Management");
    }

    public override async Task HandleAsync(UpdateClientRequest req, CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var command = new UpdateClientCommand(id, req.Name, req.Code, req.Status);
        var res = await mediator.Send(command, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendAsync<object>(null!, 204, cancellation: ct);
    }
}
