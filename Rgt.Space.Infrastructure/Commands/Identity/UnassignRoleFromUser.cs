using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class UnassignRoleFromUser
{
    public record Command(
        Guid UserId,
        Guid RoleId
    ) : IRequest<Result>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithErrorCode("USER_ID_REQUIRED");

            RuleFor(x => x.RoleId)
                .NotEmpty().WithErrorCode("ROLE_ID_REQUIRED");
        }
    }

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IUserReadDac _userReadDac;
        private readonly IRoleWriteDac _roleWriteDac;

        public Handler(IUserReadDac userReadDac, IRoleWriteDac roleWriteDac)
        {
            _userReadDac = userReadDac;
            _roleWriteDac = roleWriteDac;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // 0. Validate
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToList();
                errors.Insert(0, ErrorCatalog.VALIDATION_ERROR);
                return Result.Fail(errors);
            }

            // 1. Check user exists (optional - could be idempotent)
            var user = await _userReadDac.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            // 2. Unassign (idempotent - if not found, still success)
            await _roleWriteDac.UnassignRoleFromUserAsync(request.UserId, request.RoleId, ct);

            return Result.Ok();
        }
    }
}
