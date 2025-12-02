using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public class RevokePermission
{
    public sealed record Command(
        Guid UserId,
        string Module,
        string SubModule,
        Guid RevokedBy
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
            await _writeDac.RevokePermissionAsync(
                request.UserId,
                request.Module,
                request.SubModule,
                request.RevokedBy,
                ct);

            return Result.Ok();
        }
    }
}
