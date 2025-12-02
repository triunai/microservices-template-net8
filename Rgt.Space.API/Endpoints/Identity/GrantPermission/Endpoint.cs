using FastEndpoints;
using MediatR;
using Rgt.Space.Infrastructure.Commands.Identity;

namespace Rgt.Space.API.Endpoints.Identity.GrantPermission;

public sealed class Endpoint : Endpoint<Request>
{
    private readonly IMediator _mediator;
    private readonly Rgt.Space.Core.Abstractions.Identity.ICurrentUser _currentUser;

    public Endpoint(IMediator mediator, Rgt.Space.Core.Abstractions.Identity.ICurrentUser currentUser)
    {
        _mediator = mediator;
        _currentUser = currentUser;
    }

    public override void Configure()
    {
        Post("/api/v1/users/{UserId}/permissions/grant");
        // AllowAnonymous(); // TODO: Remove in Phase 2
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var currentUserId = _currentUser.Id; 

        var command = new Infrastructure.Commands.Identity.GrantPermission.Command(
            req.UserId,
            req.Module,
            req.SubModule,
            req.Permissions.CanView,
            req.Permissions.CanInsert,
            req.Permissions.CanEdit,
            req.Permissions.CanDelete,
            currentUserId
        );

        var result = await _mediator.Send(command, ct);

        if (result.IsFailed)
        {
            ThrowError(result.Errors.First().Message);
        }

        await Send.OkAsync(ct);
    }
}

public sealed record Request
{
    public Guid UserId { get; set; }
    public string Module { get; set; } = default!;
    public string SubModule { get; set; } = default!;
    public PermissionFlags Permissions { get; set; } = default!;
}

public sealed record PermissionFlags
{
    public bool CanView { get; set; }
    public bool CanInsert { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}
