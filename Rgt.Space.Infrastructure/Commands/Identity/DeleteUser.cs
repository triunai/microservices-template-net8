using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Identity;

public static class DeleteUser
{
    /// <summary>
    /// Result containing deletion info for frontend warning display.
    /// </summary>
    public record DeleteResult(bool Deleted, int AssignmentsRemoved);

    public record Command(Guid UserId, Guid DeletedBy) : IRequest<Result<DeleteResult>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.UserId)
                .NotEmpty().WithErrorCode("USER_ID_REQUIRED");
        }
    }

    public class Handler : IRequestHandler<Command, Result<DeleteResult>>
    {
        private readonly IUserReadDac _readDac;
        private readonly IUserWriteDac _writeDac;

        public Handler(IUserReadDac readDac, IUserWriteDac writeDac)
        {
            _readDac = readDac;
            _writeDac = writeDac;
        }

        public async Task<Result<DeleteResult>> Handle(Command request, CancellationToken ct)
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
            var existingUser = await _readDac.GetByIdAsync(request.UserId, ct);
            if (existingUser is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            // 2. Cascade: Soft-delete all project assignments for this user
            var assignmentsRemoved = await _writeDac.DeleteUserAssignmentsAsync(
                request.UserId, 
                request.DeletedBy, 
                ct);

            // 3. Soft-delete the user
            await _writeDac.DeleteAsync(request.UserId, request.DeletedBy, ct);

            return Result.Ok(new DeleteResult(true, assignmentsRemoved));
        }
    }
}
