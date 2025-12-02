using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.TaskAllocation;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Infrastructure.Commands.TaskAllocation;

public class UnassignUser
{
    public sealed record Command(Guid ProjectId, Guid UserId, string PositionCode, Guid? UnassignedBy) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ProjectId).NotEmpty().WithErrorCode("PROJECT_ID_REQUIRED");
            RuleFor(x => x.UserId).NotEmpty().WithErrorCode("USER_ID_REQUIRED");
            RuleFor(x => x.PositionCode)
                .NotEmpty().WithErrorCode("POSITION_CODE_REQUIRED")
                .Must(code => TaskAllocationConstants.Positions.All.Contains(code))
                .WithErrorCode("INVALID_POSITION_CODE")
                .WithMessage($"Position code must be one of: {string.Join(", ", TaskAllocationConstants.Positions.All)}");
            // RuleFor(x => x.UnassignedBy).NotEmpty().WithErrorCode("UNASSIGNED_BY_REQUIRED");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly ITaskAllocationWriteDac _dac;

        public Handler(ITaskAllocationWriteDac dac)
        {
            _dac = dac;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Result.Fail(validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR"));
            }

            var success = await _dac.UnassignUserAsync(
                request.ProjectId, 
                request.UserId, 
                request.PositionCode, 
                request.UnassignedBy, 
                ct);

            if (!success)
            {
                // If record not found, we can return Fail or Ok (Idempotent).
                // Usually for Delete, if it's already gone, it's a success.
                return Result.Ok();
            }

            return Result.Ok();
        }
    }
}
