using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Features;

public static class UpdateFeature
{
    public sealed record Command(
        Guid FeatureId,
        string Name,
        string? Description,
        bool IsActive,
        bool RequiresClient,
        Guid UpdatedBy
    ) : IRequest<Result>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(x => x.FeatureId)
                .NotEmpty().WithErrorCode("FEATURE_ID_REQUIRED");

            RuleFor(x => x.Name)
                .NotEmpty().WithErrorCode("FEATURE_NAME_REQUIRED")
                .MaximumLength(255).WithErrorCode("FEATURE_NAME_TOO_LONG");
        }
    }

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IFeatureReadDac _readDac;
        private readonly IFeatureWriteDac _writeDac;
        private readonly IFeatureGate _featureGate;

        public Handler(IFeatureReadDac readDac, IFeatureWriteDac writeDac, IFeatureGate featureGate)
        {
            _readDac = readDac;
            _writeDac = writeDac;
            _featureGate = featureGate;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
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

            // 1. Verify feature exists and is not deleted
            var feature = await _readDac.GetByIdAsync(request.FeatureId, ct);
            if (feature is null)
            {
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);
            }

            // 2. Update with explicit values (CRITICAL #8: full PUT, not toggle)
            // WriteDac returns false if the feature was concurrently deleted between
            // the existence check above and this write (TOCTOU race).
            var updated = await _writeDac.UpdateFeatureAsync(
                request.FeatureId, request.Name, request.Description,
                request.IsActive, request.RequiresClient,
                request.UpdatedBy, ct);

            if (!updated)
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);

            // 3. Invalidate cache
            _featureGate.InvalidateFeature(feature.Code);

            return Result.Ok();
        }
    }
}
