using FastEndpoints;
using MediatR;
using MicroservicesBase.Infrastructure.Queries.Sales;
using MicroservicesBase.Core.Domain.Contracts.Sales;
using MicroservicesBase.API.ProblemDetails;

namespace MicroservicesBase.API.Endpoints.Sales.GetById;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/sales/{id:guid}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var id = Route<Guid>("id");

        var res = await mediator.Send(new GetSaleById.GetSaleByIdQuery(id), ct);

        if (res.IsFailed)
        {
            // Convert FluentResults failure to ProblemDetails
            var problemDetails = res.ToProblemDetails(HttpContext);
            
            // Send ProblemDetails response with appropriate status code
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        // Success path
        await Send.OkAsync(res.Value, ct);
    }
}
