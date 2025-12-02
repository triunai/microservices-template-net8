using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;

namespace Rgt.Space.Infrastructure.Commands.PortalRouting;

public class DeleteMapping
{
    // 1) Command contract
    public sealed record DeleteMappingCommand(Guid Id, Guid DeletedBy) : IRequest<Result>;

    // 2) Validator
    public sealed class Validator : AbstractValidator<DeleteMappingCommand>
    {
        public Validator()
        {
            RuleFor(x => x.Id)
                .NotEqual(Guid.Empty)
                .WithErrorCode("MAPPING_ID_INVALID")
                .WithMessage("Mapping ID cannot be empty");
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<DeleteMappingCommand, Result>
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

        public async Task<Result> Handle(DeleteMappingCommand command, CancellationToken ct)
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

            // Soft Delete Mapping
            await _writeDac.SoftDeleteAsync(command.Id, command.DeletedBy, ct);

            return Result.Ok();
        }
    }
}
