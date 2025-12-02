using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;

namespace Rgt.Space.Infrastructure.Queries.Identity;

public class GetUserById
{
    public sealed record Query(Guid UserId) : IRequest<Result<UserResponse>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty()
                .WithErrorCode("USER_ID_REQUIRED")
                .WithMessage("User ID is required");
        }
    }

    public sealed class Handler : IRequestHandler<Query, Result<UserResponse>>
    {
        private readonly IUserReadDac _dac;

        public Handler(IUserReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<UserResponse>> Handle(Query request, CancellationToken ct)
        {
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Result.Fail<UserResponse>(
                    validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR"));
            }

            var user = await _dac.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                return Result.Fail<UserResponse>("USER_NOT_FOUND");
            }

            var response = new UserResponse(
                user.Id,
                user.DisplayName,
                user.Email,
                user.ContactNumber,
                user.IsActive,
                user.CreatedAt,
                user.CreatedBy,
                user.UpdatedAt,
                user.UpdatedBy);

            return Result.Ok(response);
        }
    }
}
