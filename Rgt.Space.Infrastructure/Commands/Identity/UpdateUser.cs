using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Entities.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public class UpdateUser
{
    public sealed record Command(
        Guid UserId,
        string DisplayName,
        string Email,
        string? ContactNumber,
        bool IsActive,
        Guid UpdatedBy
    ) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId).NotEmpty();
            RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IUserReadDac _readDac;
        private readonly IUserWriteDac _writeDac;

        public Handler(IUserReadDac readDac, IUserWriteDac writeDac)
        {
            _readDac = readDac;
            _writeDac = writeDac;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Result.Fail(validationResult.Errors.Select(e => e.ErrorMessage));
            }

            var existingUser = await _writeDac.GetByIdAsync(request.UserId, ct);
            if (existingUser is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            // Check if email is taken by another user
            var userWithEmail = await _readDac.GetByEmailAsync(request.Email, ct);
            if (userWithEmail != null && userWithEmail.Id != request.UserId)
            {
                return Result.Fail("Email is already in use by another user");
            }

            existingUser.UpdateDetails(
                request.DisplayName,
                request.Email,
                request.ContactNumber,
                request.IsActive,
                request.UpdatedBy);

            await _writeDac.UpdateAsync(existingUser, ct);

            return Result.Ok();
        }
    }
}
