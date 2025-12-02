using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public class GrantPermission
{
    public sealed record Command(
        Guid UserId,
        string Module,
        string SubModule,
        bool CanView,
        bool CanInsert,
        bool CanEdit,
        bool CanDelete,
        Guid GrantedBy
    ) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.Module).NotEmpty();
            RuleFor(x => x.SubModule).NotEmpty();
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IUserWriteDac _writeDac;

        public Handler(IUserWriteDac writeDac)
        {
            _writeDac = writeDac;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var success = await _writeDac.GrantPermissionAsync(
                request.UserId,
                request.Module,
                request.SubModule,
                request.CanView,
                request.CanInsert,
                request.CanEdit,
                request.CanDelete,
                request.GrantedBy,
                ct);

            if (!success)
            {
                return Result.Fail("Failed to grant permission. Ensure Module and SubModule codes are correct.");
            }

            return Result.Ok();
        }
    }
}
