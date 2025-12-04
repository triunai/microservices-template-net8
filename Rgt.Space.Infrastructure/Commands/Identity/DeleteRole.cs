using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class DeleteRole
{
    public record Command(Guid RoleId) : IRequest<Result>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RoleId)
                .NotEmpty().WithErrorCode("ROLE_ID_REQUIRED");
        }
    }

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IRoleReadDac _readDac;
        private readonly IRoleWriteDac _writeDac;

        public Handler(IRoleReadDac readDac, IRoleWriteDac writeDac)
        {
            _readDac = readDac;
            _writeDac = writeDac;
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

            // 1. Check existence
            var existingRole = await _readDac.GetByIdAsync(request.RoleId, ct);
            if (existingRole is null)
            {
                return Result.Fail(ErrorCatalog.ROLE_NOT_FOUND);
            }

            // 2. Business rule: Cannot delete system roles
            if (existingRole.IsSystem)
            {
                return Result.Fail(ErrorCatalog.ROLE_IS_SYSTEM);
            }

            // 3. Business rule: Cannot delete role with assigned users
            if (existingRole.UserCount > 0)
            {
                return Result.Fail(ErrorCatalog.ROLE_HAS_USERS);
            }

            // 4. Delete (hard delete for roles)
            await _writeDac.DeleteAsync(request.RoleId, ct);

            return Result.Ok();
        }
    }
}
