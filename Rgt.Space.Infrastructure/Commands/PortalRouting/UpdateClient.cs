using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.PortalRouting;


namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public static class UpdateClient
{
    public record Command(Guid Id, string Name, string Code, string Status) : IRequest<Result>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();

            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(255);

            RuleFor(x => x.Code)
                .NotEmpty()
                .MaximumLength(50)
                .Matches("^[A-Z0-9_]+$").WithMessage("Client Code must contain only uppercase letters, numbers, and underscores.");

            RuleFor(x => x.Status)
                .Must(s => s == "Active" || s == "Inactive")
                .WithMessage("Status must be 'Active' or 'Inactive'.");
        }
    }

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IClientReadDac _readDac;
        private readonly IClientWriteDac _writeDac;
        private readonly ICurrentUser _currentUser;

        public Handler(IClientReadDac readDac, IClientWriteDac writeDac, ICurrentUser currentUser)
        {
            _readDac = readDac;
            _writeDac = writeDac;
            _currentUser = currentUser;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // 0. Validate
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.Errors.Select(e => e.ErrorMessage).ToList();
                errors.Insert(0, Core.Errors.ErrorCatalog.VALIDATION_ERROR);
                return Result.Fail(errors);
            }

            // 1. Check existence
            var existing = await _readDac.GetByIdAsync(request.Id, ct);
            if (existing is null)
            {
                return Result.Fail("CLIENT_NOT_FOUND");
            }

            // 2. Check uniqueness if code changed
            if (existing.Code != request.Code)
            {
                var duplicate = await _readDac.GetByCodeAsync(request.Code, ct);
                if (duplicate is not null)
                {
                    return Result.Fail("ROUTING_CLIENT_CODE_EXISTS");
                }
            }

            var userId = _currentUser.Id;

            // 3. Update
            await _writeDac.UpdateAsync(
                request.Id,
                request.Name,
                request.Code,
                request.Status,
                userId,
                ct);

            return Result.Ok();
        }
    }
}
