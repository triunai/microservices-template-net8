using FastEndpoints;
using MediatR;
using Rgt.Space.Infrastructure.Queries.Sales;
using Rgt.Space.Core.Domain.Contracts.Sales;
using Rgt.Space.API.ProblemDetails;

namespace Rgt.Space.API.Endpoints.Sales.GetById;

public sealed class Endpoint(IMediator mediator) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("/api/v1/sales/{id:guid}");
        AllowAnonymous();
        
        Summary(s =>
        {
            s.Summary = "Get sale by ID (v1)";
            s.Description = "Retrieves a sale record by its unique identifier for the authenticated tenant. API Version 1.0";
            s.Response(200, "Sale found and returned successfully");
            s.Response(404, "Sale not found for the given ID");
        });
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
