using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Infrastructure.Queries.Identity;

namespace Rgt.Space.API.Endpoints.Identity.GetUsers;

public class Endpoint : Endpoint<GetAllUsers.Query>
{
    private readonly IMediator _mediator;

    public Endpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/users");
        // AllowAnonymous(); // TODO: Auth
        
        Summary(s =>
        {
            s.Summary = "Get all users";
            s.Description = "Returns a list of all active users, optionally filtered by search term.";
            s.Response<IReadOnlyList<UserResponse>>(200, "List of users");
        });
    }

    public override async Task HandleAsync(GetAllUsers.Query req, CancellationToken ct)
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
