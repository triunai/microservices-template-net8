using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Abstractions.PortalRouting;

namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public static class DeleteClient
{
    public record Command(Guid Id) : IRequest<Result>;

    public class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.Id).NotEmpty();
        }
    }

    public class Handler : IRequestHandler<Command, Result>
    {
        private readonly IClientReadDac _readDac;
        private readonly IClientWriteDac _writeDac;
        private readonly IProjectReadDac _projectReadDac;
        private readonly ICurrentUser _currentUser;

        public Handler(
            IClientReadDac readDac,
            IClientWriteDac writeDac,
            IProjectReadDac projectReadDac,
            ICurrentUser currentUser)
        {
            _readDac = readDac;
            _writeDac = writeDac;
            _projectReadDac = projectReadDac;
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

            // 2. Check for active projects (RESTRICT rule)
            var projects = await _projectReadDac.GetByClientIdAsync(request.Id, ct);
            if (projects.Any())
            {
                return Result.Fail("CLIENT_HAS_PROJECTS");
            }

            // 3. Delete
            var userId = _currentUser.Id;
            await _writeDac.DeleteAsync(request.Id, userId, ct);

            return Result.Ok();
        }
    }
}
