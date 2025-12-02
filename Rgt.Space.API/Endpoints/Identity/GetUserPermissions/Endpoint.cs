using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Infrastructure.Queries.Identity;

namespace Rgt.Space.API.Endpoints.Identity.GetUserPermissions;

public class Endpoint : EndpointWithoutRequest<IReadOnlyList<UserPermissionResponse>>
{
    private readonly IMediator _mediator;

    public Endpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/v1/users/{userId:guid}/permissions");
        // AllowAnonymous(); // TODO: Auth
        
        Summary(s =>
        {
            s.Summary = "Get user permissions";
            s.Description = "Retrieves the effective permissions for a specific user.";
            s.Response<IReadOnlyList<UserPermissionResponse>>(200, "Permissions returned successfully");
        });
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Route<Guid>("userId");
        var result = await _mediator.Send(new Rgt.Space.Infrastructure.Queries.Identity.GetUserPermissions.Query(userId), ct);

        if (result.IsFailed)
        {
            var problemDetails = result.ToProblemDetails(HttpContext);
            // When using Endpoint<TResponse>, SendAsync expects TResponse. 
            // For errors, we should use the built-in ProblemDetails support or untyped SendAsync.
            // FastEndpoints allows sending any object if we don't specify type in SendAsync, 
            // but here we are in a typed endpoint.
            // SendResultAsync is not available in this version or context.
            // Fallback to writing directly to response which is always safe.
            HttpContext.Response.StatusCode = problemDetails.Status ?? 500;
            await HttpContext.Response.WriteAsJsonAsync(problemDetails, ct);
            return;
        }

        await Send.OkAsync(result.Value, ct);
    }
}
