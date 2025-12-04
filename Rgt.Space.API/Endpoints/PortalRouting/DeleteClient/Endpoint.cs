using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using DeleteClientCommand = Rgt.Space.Infrastructure.Commands.PortalRouting.DeleteClient.Command;

namespace Rgt.Space.API.Endpoints.PortalRouting.DeleteClient;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/portal-routing/clients/{id}");
        Summary(s =>
        {
            s.Summary = "Delete a client";
            s.Description = "Soft deletes a client. Requires client to have no active projects.";
            s.Response(204, "Client deleted successfully");
            s.Response(404, "Client not found");
            s.Response(409, "Client has active projects");
        });
        Tags("Client Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");
        var command = new DeleteClientCommand(id);
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
