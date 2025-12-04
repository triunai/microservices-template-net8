using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;
using DeleteUserCommand = Rgt.Space.Infrastructure.Commands.Identity.DeleteUser.Command;

namespace Rgt.Space.API.Endpoints.Identity.DeleteUser;

public sealed class Endpoint(IMediator mediator, ICurrentUser currentUser) : EndpointWithoutRequest
{
    public override void Configure()
    {
        Delete("/api/v1/users/{userId}");
        Summary(s =>
        {
            s.Summary = "Delete a user";
            s.Description = "Soft deletes a user and cascade deletes all their project assignments. Returns count of deleted assignments for frontend warning display.";
            s.Response<DeleteUserResponse>(200, "User deleted successfully");
            s.Response(404, "User not found");
        });
        Tags("User Management");
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        
        var command = new DeleteUserCommand(userId, currentUser.Id);
        var result = await mediator.Send(command, ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        var response = new DeleteUserResponse(
            result.Value.Deleted,
            result.Value.AssignmentsRemoved);

        await Send.OkAsync(response, ct);
    }
}
