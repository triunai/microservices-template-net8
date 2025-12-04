using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class UpdateRole
{
    public record Command(
        Guid RoleId,
        string Name,
        string? Description,
        bool IsActive,
        Guid UpdatedBy
    ) : IRequest<Result>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.RoleId)
                .NotEmpty().WithErrorCode("ROLE_ID_REQUIRED");

            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("ROLE_NAME_REQUIRED")
                .MaximumLength(100).WithErrorCode("ROLE_NAME_TOO_LONG");
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

            // 2. Business rule: System roles cannot be deactivated
            if (existingRole.IsSystem && !request.IsActive)
            {
                return Result.Fail(ErrorCatalog.ROLE_IS_SYSTEM);
            }

            // 3. Update (code cannot be changed)
            await _writeDac.UpdateAsync(
                request.RoleId,
                request.Name,
                request.Description,
                request.IsActive,
                request.UpdatedBy,
                ct);

            return Result.Ok();
        }
    }
}
