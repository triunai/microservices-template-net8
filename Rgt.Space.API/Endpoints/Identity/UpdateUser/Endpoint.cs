using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
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

    public Endpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/v1/users/{userId:guid}");
        AllowAnonymous(); // TODO: Auth
        
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
        // TODO: Extract from claims. For now, use the user's own ID to satisfy FK constraint if self-updating, 
        // or a known system/admin ID. Since we don't have auth yet, let's use the target user's ID as a fallback 
        // assuming they are updating themselves, or a hardcoded system ID if available.
        // The error 23503 indicates `updated_by` must exist in `users` table.
        // Guid.Empty definitely doesn't exist.
        Guid updatedBy = req.UserId;

        var cmd = new Rgt.Space.Infrastructure.Commands.Identity.UpdateUser.Command(
            req.UserId,
            req.DisplayName,
            req.Email,
            req.ContactNumber,
            req.IsActive,
            updatedBy);

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
