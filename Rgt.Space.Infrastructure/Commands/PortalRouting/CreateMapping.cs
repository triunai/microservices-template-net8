using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;

namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public class CreateMapping
{
    // 1) Command contract
    public sealed record CreateMappingCommand(
        Guid ProjectId,
        string RoutingUrl,
        string Environment
    ) : IRequest<Result<Guid>>;

    // 2) Validator
    public sealed class Validator : AbstractValidator<CreateMappingCommand>
    {
        public Validator()
        {
            RuleFor(x => x.ProjectId)
                .NotEqual(Guid.Empty)
                .WithErrorCode("PROJECT_ID_INVALID")
                .WithMessage("Project ID cannot be empty");

            RuleFor(x => x.RoutingUrl)
                .NotEmpty()
                .Matches(@"^/[a-z0-9_-]+(/[a-z0-9_-]+)*$")
                .WithErrorCode("INVALID_ROUTING_URL")
                .WithMessage("Routing URL must start with / and contain only lowercase letters, numbers, underscores, and hyphens");

            RuleFor(x => x.Environment)
                .NotEmpty()
                .Must(env => new[] { "Production", "Staging", "Development", "UAT" }.Contains(env))
                .WithErrorCode("INVALID_ENVIRONMENT")
                .WithMessage("Environment must be one of: Production, Staging, Development, UAT");
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<CreateMappingCommand, Result<Guid>>
    {
        private readonly IClientProjectMappingWriteDac _writeDac;
        private readonly IClientProjectMappingReadDac _readDac;
        private readonly IProjectReadDac _projectReadDac;

        public Handler(
            IClientProjectMappingWriteDac writeDac,
            IClientProjectMappingReadDac readDac,
            IProjectReadDac projectReadDac)
        {
            _writeDac = writeDac;
            _readDac = readDac;
            _projectReadDac = projectReadDac;
        }

        public async Task<Result<Guid>> Handle(CreateMappingCommand command, CancellationToken ct)
        {
            // Validate inline
            var v = new Validator();
            var vr = await v.ValidateAsync(command, ct);
            if (!vr.IsValid)
                return Result.Fail<Guid>(
                    vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToArray());

            // Business Rule: Project must exist
            var project = await _projectReadDac.GetByIdAsync(command.ProjectId, ct);
            if (project is null)
                return Result.Fail<Guid>("PROJECT_NOT_FOUND");

            // Business Rule: Routing URL must be globally unique
            var existingMapping = await _readDac.GetByRoutingUrlAsync(command.RoutingUrl, ct);
            if (existingMapping is not null)
                return Result.Fail<Guid>("ROUTING_URL_ALREADY_EXISTS");

            // Create Mapping
            var id = await _writeDac.CreateAsync(
                command.ProjectId,
                command.RoutingUrl,
                command.Environment,
                ct);

            return Result.Ok(id);
        }
    }
}
