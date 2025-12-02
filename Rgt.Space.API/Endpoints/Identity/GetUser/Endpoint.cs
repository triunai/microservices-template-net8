using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Infrastructure.Queries.Identity;

namespace Rgt.Space.API.Endpoints.Identity.GetUser;

public class Endpoint : Endpoint<GetUserById.Query>
{
    private readonly IMediator _mediator;

    public Endpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/users/{userId:guid}");
        // AllowAnonymous(); // TODO: Auth
        
        Summary(s =>
        {
            s.Summary = "Get user by ID";
            s.Description = "Returns details of a specific user.";
            s.Response<UserResponse>(200, "User details");
            s.Response(404, "User not found");
        });
    }

    public override async Task HandleAsync(GetUserById.Query req, CancellationToken ct)
    {
        var result = await _mediator.Send(req, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
