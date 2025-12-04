using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;
using CreateUserCommand = Rgt.Space.Infrastructure.Commands.Identity.CreateUser.Command;

namespace Rgt.Space.API.Endpoints.Identity.CreateUser;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : Endpoint<CreateUserRequest>
{
    public override void Configure()
    {
        Post("/api/v1/users");
        Summary(s =>
        {
            s.Summary = "Create a new user";
            s.Description = "Creates a new user with optional local login credentials. If localLoginEnabled is true, password is required.";
            s.Response<CreateUserResponse>(201, "User created successfully");
            s.Response(400, "Validation failure or password required");
            s.Response(409, "Email already exists");
        });
        Tags("User Management");
    }

    public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
    {
        var command = new CreateUserCommand(
            req.DisplayName,
            req.Email,
            req.ContactNumber,
            req.LocalLoginEnabled,
            req.Password,
            req.RoleIds,
            currentUser.Id);

        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        // Build response
        var response = new CreateUserResponse(
            Id: result.Value,
            DisplayName: req.DisplayName,
            Email: req.Email,
            ContactNumber: req.ContactNumber,
            IsActive: true,
            LocalLoginEnabled: req.LocalLoginEnabled,
            SsoLoginEnabled: false,
            Roles: null, // Role assignment is Phase 3
            CreatedAt: DateTime.UtcNow);

        await HttpContext.Response.SendCreatedAtAsync<GetUser.Endpoint>(
            new { userId = result.Value },
            response,
            cancellation: ct);
    }
}
