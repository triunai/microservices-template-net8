using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Features;

public static class UpsertClientFeature
{
    public sealed record Command(
        Guid ClientId,
        Guid FeatureId,
        bool IsEnabled,
        Guid UpdatedBy
    ) : IRequest<Result>;

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IFeatureReadDac _featureReadDac;
        private readonly IFeatureWriteDac _featureWriteDac;
        private readonly IClientReadDac _clientReadDac;
        private readonly IFeatureGate _featureGate;

        public Handler(
            IFeatureReadDac featureReadDac,
            IFeatureWriteDac featureWriteDac,
            IClientReadDac clientReadDac,
            IFeatureGate featureGate)
        {
            _featureReadDac = featureReadDac;
            _featureWriteDac = featureWriteDac;
            _clientReadDac = clientReadDac;
            _featureGate = featureGate;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // 1. Verify feature exists
            var feature = await _featureReadDac.GetByIdAsync(request.FeatureId, ct);
            if (feature is null)
            {
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);
            }

            // 2. Verify client exists (is_deleted = FALSE filtered by DAC)
            var client = await _clientReadDac.GetByIdAsync(request.ClientId, ct);
            if (client is null)
            {
                return Result.Fail(ErrorCatalog.CLIENT_NOT_FOUND);
            }

            // 3. Upsert with explicit isEnabled (CRITICAL #8: not toggle)
            await _featureWriteDac.UpsertClientFeatureAsync(
                request.ClientId, request.FeatureId,
                request.IsEnabled, request.UpdatedBy, ct);

            // 4. Invalidate cache
            _featureGate.InvalidateClientFeature(request.ClientId, request.FeatureId);

            return Result.Ok();
        }
    }
}
