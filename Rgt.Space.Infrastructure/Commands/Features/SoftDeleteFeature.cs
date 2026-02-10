using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Features;

public static class SoftDeleteFeature
{
    public sealed record Command(
        Guid FeatureId,
        Guid DeletedBy
    ) : IRequest<Result>;

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
            // 1. Verify feature exists
            var feature = await _readDac.GetByIdAsync(request.FeatureId, ct);
            if (feature is null)
            {
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);
            }

            // 2. Soft delete — returns false if concurrently deleted (TOCTOU race)
            var deleted = await _writeDac.SoftDeleteFeatureAsync(request.FeatureId, request.DeletedBy, ct);

            if (!deleted)
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);

            // 3. Invalidate cache
            _featureGate.InvalidateFeature(feature.Code);

            return Result.Ok();
        }
    }
}
