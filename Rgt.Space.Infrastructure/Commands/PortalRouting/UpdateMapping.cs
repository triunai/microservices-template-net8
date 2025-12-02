using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Constants;

namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public class UpdateMapping
{
    // 1) Command contract
    public sealed record UpdateMappingCommand(
        Guid Id,
        string RoutingUrl,
        string Environment,
        string Status,
        Guid UpdatedBy
    ) : IRequest<Result>;

    // 2) Validator
    public sealed class Validator : AbstractValidator<UpdateMappingCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .NotEqual(Guid.Empty)
                .WithErrorCode("MAPPING_ID_INVALID")
                .WithMessage("Mapping ID cannot be empty");

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

            RuleFor(x => x.Status)
                .NotEmpty()
                .Must(status => StatusConstants.All.Contains(status))
                .WithErrorCode("INVALID_STATUS")
                .WithMessage("Status must be one of: Active, Inactive");
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<UpdateMappingCommand, Result>
    {
        private readonly IClientProjectMappingWriteDac _writeDac;
        private readonly IClientProjectMappingReadDac _readDac;

        public Handler(
            IClientProjectMappingWriteDac writeDac,
            IClientProjectMappingReadDac readDac)
        {
            _writeDac = writeDac;
            _readDac = readDac;
        }

        public async Task<Result> Handle(UpdateMappingCommand command, CancellationToken ct)
        {
            // Validate inline
            var v = new Validator();
            var vr = await v.ValidateAsync(command, ct);
            if (!vr.IsValid)
                return Result.Fail(
                    vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToArray());

            // Business Rule: Mapping must exist
            var existingMapping = await _readDac.GetByIdAsync(command.Id, ct);
            if (existingMapping is null)
                return Result.Fail("MAPPING_NOT_FOUND");

            // Business Rule: If Routing URL changed, check uniqueness
            if (existingMapping.RoutingUrl != command.RoutingUrl)
            {
                var duplicate = await _readDac.GetByRoutingUrlAsync(command.RoutingUrl, ct);
                if (duplicate is not null)
                    return Result.Fail("ROUTING_URL_ALREADY_EXISTS");
            }

            // Update Mapping
            await _writeDac.UpdateAsync(
                command.Id,
                command.RoutingUrl,
                command.Environment,
                command.Status,
                command.UpdatedBy,
                ct);

            return Result.Ok();
        }
    }
}
