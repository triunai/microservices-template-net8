using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.TaskAllocation;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Infrastructure.Commands.TaskAllocation;

public class UpdateAssignment
{
    public sealed record Command(Guid ProjectId, Guid UserId, string OldPositionCode, string NewPositionCode, Guid? UpdatedBy) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ProjectId).NotEmpty().WithErrorCode("PROJECT_ID_REQUIRED");
            RuleFor(x => x.UserId).NotEmpty().WithErrorCode("USER_ID_REQUIRED");
            
            RuleFor(x => x.OldPositionCode)
                .NotEmpty().WithErrorCode("OLD_POSITION_CODE_REQUIRED")
                .Must(code => TaskAllocationConstants.Positions.All.Contains(code))
                .WithErrorCode("INVALID_POSITION_CODE")
                .WithMessage($"Old position code must be one of: {string.Join(", ", TaskAllocationConstants.Positions.All)}");

            RuleFor(x => x.NewPositionCode)
                .NotEmpty().WithErrorCode("NEW_POSITION_CODE_REQUIRED")
                .Must(code => TaskAllocationConstants.Positions.All.Contains(code))
                .WithErrorCode("INVALID_POSITION_CODE")
                .WithMessage($"New position code must be one of: {string.Join(", ", TaskAllocationConstants.Positions.All)}");

            RuleFor(x => x.NewPositionCode)
                .NotEqual(x => x.OldPositionCode)
                .WithErrorCode("NEW_POSITION_SAME_AS_OLD")
                .WithMessage("New position must be different from old position.");
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

            var success = await _dac.UpdateAssignmentAsync(
                request.ProjectId, 
                request.UserId, 
                request.OldPositionCode, 
                request.NewPositionCode, 
                request.UpdatedBy, 
                ct);

            if (!success)
            {
                // If update failed (likely old assignment not found), return failure
                return Result.Fail("ASSIGNMENT_NOT_FOUND");
            }

            return Result.Ok();
        }
    }
}
