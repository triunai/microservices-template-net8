using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Utilities;

namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public static class CreateProject
{
    public record Command(Guid ClientId, string Name, string Code, string Status, string? ExternalUrl) : IRequest<Result<Guid>>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.ClientId)
                .NotEmpty();

            RuleFor(x => x.Name)
                .NotEmpty()
                .MaximumLength(255);

            RuleFor(x => x.Code)
                .NotEmpty()
                .MaximumLength(50)
                .Matches("^[A-Z0-9_]+$").WithMessage("Project Code must contain only uppercase letters, numbers, and underscores.");

            RuleFor(x => x.Status)
                .Must(s => s == "Active" || s == "Inactive")
                .WithMessage("Status must be 'Active' or 'Inactive'.");
        }
    }

    public class Handler : IRequestHandler<Command, Result<Guid>>
    {
        private readonly IClientReadDac _clientReadDac;
        private readonly IProjectReadDac _projectReadDac;
        private readonly IProjectWriteDac _writeDac;
        private readonly ICurrentUser _currentUser;

        public Handler(
            IClientReadDac clientReadDac,
            IProjectReadDac projectReadDac,
            IProjectWriteDac writeDac,
            ICurrentUser currentUser)
        {
            _clientReadDac = clientReadDac;
            _projectReadDac = projectReadDac;
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

            // 1. Check Client exists
            var client = await _clientReadDac.GetByIdAsync(request.ClientId, ct);
            if (client is null)
            {
                return Result.Fail("CLIENT_NOT_FOUND");
            }

            // 2. Check uniqueness
            var existing = await _projectReadDac.GetByClientAndCodeAsync(request.ClientId, request.Code, ct);
            if (existing is not null)
            {
                return Result.Fail("PROJECT_CODE_EXISTS_IN_CLIENT");
            }

            // 3. Generate ID
            var id = Uuid7.NewUuid7();
            var userId = _currentUser.Id;

            // 4. Create
            await _writeDac.CreateAsync(
                id,
                request.ClientId,
                request.Name,
                request.Code,
                request.Status,
                request.ExternalUrl,
                userId,
                ct);

            return Result.Ok(id);
        }
    }
}
