using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.PortalRouting;

using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public static class CreateClient
{
    public record Command(string Name, string Code, string Status) : IRequest<Result<Guid>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
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

    public class Handler : IRequestHandler<Command, Result<Guid>>
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

        public async Task<Result<Guid>> Handle(Command request, CancellationToken ct)
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

            // 1. Check uniqueness
            var existing = await _readDac.GetByCodeAsync(request.Code, ct);
            if (existing is not null)
            {
                return Result.Fail("ROUTING_CLIENT_CODE_EXISTS");
            }

            // 2. Generate ID
            var id = Uuid7.NewUuid7();
            var userId = _currentUser.Id;

            // 3. Create
            await _writeDac.CreateAsync(
                id,
                request.Name,
                request.Code,
                request.Status,
                userId,
                ct);

            return Result.Ok(id);
        }
    }
}
