using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using LoginCommand = Rgt.Space.Infrastructure.Commands.Auth.Login;

namespace Rgt.Space.API.Endpoints.Auth.Login;

public class LoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class LoginResponse
{
    public string AccessToken { get; set; } = default!;
    public string RefreshToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public string Email { get; set; } = default!;
}

public sealed class Endpoint(IMediator mediator) : Endpoint<LoginRequest, LoginResponse>
{
    public override void Configure()
    {
        Post("/api/v1/auth/login");
        AllowAnonymous(); // Login doesn't require authentication
        Summary(s =>
        {
            s.Summary = "Local login with email and password";
            s.Description = "Authenticates a user with email/password and returns JWT tokens";
            s.Response<LoginResponse>(200, "Login successful");
            s.Response(400, "Validation failure");
            s.Response(401, "Invalid credentials");
            s.Response(403, "Account disabled or local login not enabled");
        });
        Tags("Authentication");
    }

    public override async Task HandleAsync(LoginRequest req, CancellationToken ct)
    {
        var command = new LoginCommand.Command(req.Email, req.Password);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        var response = new LoginResponse
        {
            AccessToken = result.Value.AccessToken,
            RefreshToken = result.Value.RefreshToken,
            ExpiresAt = result.Value.ExpiresAt,
            UserId = result.Value.UserId.ToString(),
            DisplayName = result.Value.DisplayName,
            Email = result.Value.Email
        };

        await HttpContext.Response.SendAsync(response, 200, cancellation: ct);
    }
}
