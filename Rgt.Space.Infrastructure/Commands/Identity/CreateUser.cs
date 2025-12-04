using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class CreateUser
{
    public record Command(
        string DisplayName,
        string Email,
        string? ContactNumber,
        bool LocalLoginEnabled,
        string? Password,
        List<Guid>? RoleIds,
        Guid CreatedBy
    ) : IRequest<Result<Guid>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.DisplayName)
                .NotEmpty().WithErrorCode("DISPLAY_NAME_REQUIRED")
                .MaximumLength(100).WithErrorCode("DISPLAY_NAME_TOO_LONG");

            RuleFor(x => x.Email)
                .NotEmpty().WithErrorCode("EMAIL_REQUIRED")
                .EmailAddress().WithErrorCode("EMAIL_FORMAT_INVALID");

            // Password required only if local login enabled
            RuleFor(x => x.Password)
                .NotEmpty()
                .When(x => x.LocalLoginEnabled)
                .WithErrorCode("USER_PASSWORD_REQUIRED")
                .WithMessage("Password is required when local login is enabled");
        }
    }

    public class Handler : IRequestHandler<Command, Result<Guid>>
    {
        private readonly IUserReadDac _readDac;
        private readonly IUserWriteDac _writeDac;

        public Handler(IUserReadDac readDac, IUserWriteDac writeDac)
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

            // 1. Check email uniqueness (only among non-deleted users)
            var existingUser = await _readDac.GetByEmailAsync(request.Email, ct);
            if (existingUser is not null)
            {
                return Result.Fail(ErrorCatalog.USER_EMAIL_EXISTS);
            }

            // 2. Hash password if provided
            byte[]? passwordHash = null;
            byte[]? passwordSalt = null;
            if (request.LocalLoginEnabled && !string.IsNullOrEmpty(request.Password))
            {
                var (hash, salt) = PasswordHasher.HashPassword(request.Password);
                passwordHash = hash;
                passwordSalt = salt;
            }

            // 3. Create user entity
            var user = User.CreateManual(
                request.DisplayName,
                request.Email,
                request.ContactNumber,
                request.LocalLoginEnabled,
                passwordHash,
                passwordSalt,
                request.CreatedBy);

            // 4. Persist
            var userId = await _writeDac.CreateAsync(user, ct);

            // TODO: If roleIds provided, assign roles (Phase 3)
            // This will be implemented in User-Role Assignment phase

            return Result.Ok(userId);
        }
    }
}
