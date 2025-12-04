using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class CreateRole
{
    public record Command(
        string Name,
        string Code,
        string? Description,
        bool IsActive,
        Guid CreatedBy
    ) : IRequest<Result<Guid>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("ROLE_NAME_REQUIRED")
                .MaximumLength(100).WithErrorCode("ROLE_NAME_TOO_LONG");

            RuleFor(x => x.Code)
                .NotEmpty().WithErrorCode("ROLE_CODE_REQUIRED")
                .MaximumLength(50).WithErrorCode("ROLE_CODE_TOO_LONG")
                .Matches("^[A-Z0-9_]+$").WithErrorCode("ROLE_CODE_FORMAT_INVALID")
                .WithMessage("Role code must contain only uppercase letters, numbers, and underscores.");
        }
    }

    public class Handler : IRequestHandler<Command, Result<Guid>>
    {
        private readonly IRoleReadDac _readDac;
        private readonly IRoleWriteDac _writeDac;

        public Handler(IRoleReadDac readDac, IRoleWriteDac writeDac)
        {
            _readDac = readDac;
            _writeDac = writeDac;
        }

        public async Task<Result<Guid>> Handle(Command request, CancellationToken ct)
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

            // 1. Check code uniqueness
            var existingRole = await _readDac.GetByCodeAsync(request.Code, ct);
            if (existingRole is not null)
            {
                return Result.Fail(ErrorCatalog.ROLE_CODE_EXISTS);
            }

            // 2. Generate ID and create
            var id = Uuid7.NewUuid7();
            await _writeDac.CreateAsync(
                id,
                request.Name,
                request.Code,
                request.Description,
                request.IsActive,
                request.CreatedBy,
                ct);

            return Result.Ok(id);
        }
    }
}
