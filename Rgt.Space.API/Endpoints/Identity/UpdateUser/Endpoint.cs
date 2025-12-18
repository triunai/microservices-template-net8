using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Infrastructure.Commands.Identity;

namespace Rgt.Space.API.Endpoints.Identity.UpdateUser;

public class UpdateUserRequest
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string? ContactNumber { get; set; }
    public bool IsActive { get; set; }
}

public class Endpoint : Endpoint<UpdateUserRequest>
{
    private readonly IMediator _mediator;
    private readonly ICurrentUser _currentUser;

    public Endpoint(IMediator mediator, ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public override void Configure()
    {
        Put("/api/v1/users/{userId:guid}");
        
        Summary(s =>
        {
            s.Summary = "Update user details";
            s.Description = "Updates a user's profile information.";
            s.Response(200, "User updated successfully");
            s.Response(400, "Validation error");
            s.Response(404, "User not found");
        });
    }

    public override async Task HandleAsync(UpdateUserRequest req, CancellationToken ct)
    {
        var cmd = new Rgt.Space.Infrastructure.Commands.Identity.UpdateUser.Command(
            req.UserId,
            req.DisplayName,
            req.Email,
            req.ContactNumber,
            req.IsActive,
            _currentUser.Id);

        var result = await _mediator.Send(cmd, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await Send.ResponseAsync(problemDetails, problemDetails.Status ?? 500, ct);
            return;
        }

        await Send.OkAsync(ct);
    }
}
