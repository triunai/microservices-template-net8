using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class AssignRoleToUser
{
    public record AssignRoleResult(Guid? UserRoleId, bool WasCreated);

    public record Command(
        Guid UserId,
        Guid RoleId,
        Guid AssignedBy
    ) : IRequest<Result<AssignRoleResult>>;

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

    public class Handler : IRequestHandler<Command, Result<AssignRoleResult>>
    {
        private readonly IUserReadDac _userReadDac;
        private readonly IRoleReadDac _roleReadDac;
        private readonly IRoleWriteDac _roleWriteDac;

        public Handler(IUserReadDac userReadDac, IRoleReadDac roleReadDac, IRoleWriteDac roleWriteDac)
        {
            _userReadDac = userReadDac;
            _roleReadDac = roleReadDac;
            _roleWriteDac = roleWriteDac;
        }

        public async Task<Result<AssignRoleResult>> Handle(Command request, CancellationToken ct)
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

            // 1. Check user exists
            var user = await _userReadDac.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            // 2. Check role exists
            var role = await _roleReadDac.GetByIdAsync(request.RoleId, ct);
            if (role is null)
            {
                return Result.Fail(ErrorCatalog.ROLE_NOT_FOUND);
            }

            // 3. Assign role (idempotent - returns null if already exists)
            var userRoleId = await _roleWriteDac.AssignRoleToUserAsync(
                request.UserId,
                request.RoleId,
                request.AssignedBy,
                ct);

            return Result.Ok(new AssignRoleResult(userRoleId, userRoleId.HasValue));
        }
    }
}
