using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using CreateClientCommand = Rgt.Space.Infrastructure.Commands.PortalRouting.CreateClient.Command;

namespace Rgt.Space.API.Endpoints.PortalRouting.CreateClient;

public sealed class Endpoint(IMediator mediator) : Endpoint<CreateClientRequest>
{
    public override void Configure()
    {
        Post("/api/v1/portal-routing/clients");
        Summary(s =>
        {
            s.Summary = "Create a new client";
            s.Description = "Creates a new client organization";
            s.Response(201, "Client created successfully");
            s.Response(400, "Validation failure");
            s.Response(409, "Client code already exists");
        });
        Tags("Client Management");
    }

    public override async Task HandleAsync(CreateClientRequest req, CancellationToken ct)
    {
        var command = new CreateClientCommand(req.Name, req.Code, req.Status);
        var res = await mediator.Send(command, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await HttpContext.Response.SendCreatedAtAsync<GetClientById.Endpoint>(
            new { Id = res.Value },
            new { Id = res.Value },
            cancellation: ct);
    }
}
