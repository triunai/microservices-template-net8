using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;

namespace Rgt.Space.Infrastructure.Commands.Features;

public static class ClearUserOverride
{
    public sealed record Command(
        Guid UserId,
        string FeatureCode
    ) : IRequest<Result>;

    public sealed class Handler : IRequestHandler<Command, Result>
    {
        private readonly IFeatureReadDac _featureReadDac;
        private readonly IFeatureWriteDac _featureWriteDac;
        private readonly IUserReadDac _userReadDac;
        private readonly IFeatureGate _featureGate;

        public Handler(
            IFeatureReadDac featureReadDac,
            IFeatureWriteDac featureWriteDac,
            IUserReadDac userReadDac,
            IFeatureGate featureGate)
        {
            _featureReadDac = featureReadDac;
            _featureWriteDac = featureWriteDac;
            _userReadDac = userReadDac;
            _featureGate = featureGate;
        }

        public async Task<Result> Handle(Command request, CancellationToken ct)
        {
            // 1. Look up feature by code
            var code = request.FeatureCode.Trim().ToUpperInvariant();
            var feature = await _featureReadDac.GetByCodeAsync(code, ct);
            if (feature is null)
            {
                return Result.Fail(ErrorCatalog.FEATURE_NOT_FOUND);
            }

            // 2. Verify user exists (consistent with SetUserOverride)
            var user = await _userReadDac.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            // 3. Clear override (hard delete)
            await _featureWriteDac.ClearUserOverrideAsync(request.UserId, feature.Id, ct);

            // 4. Invalidate cache
            _featureGate.InvalidateUserOverride(request.UserId, feature.Id);

            return Result.Ok();
        }
    }
}
