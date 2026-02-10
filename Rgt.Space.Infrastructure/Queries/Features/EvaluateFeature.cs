using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class EvaluateFeature
{
    public sealed record Query(
        string FeatureCode,
        Guid UserId,
        Guid? ClientId) : IRequest<Result<FeatureDecision>>;

    public sealed class Handler : IRequestHandler<Query, Result<FeatureDecision>>
    {
        private readonly IFeatureGate _featureGate;

        public Handler(IFeatureGate featureGate)
        {
            _featureGate = featureGate;
        }

        public async Task<Result<FeatureDecision>> Handle(Query request, CancellationToken ct)
        {
            var decision = await _featureGate.IsEnabledAsync(
                request.FeatureCode,
                request.ClientId ?? Guid.Empty,
                request.UserId,
                ct);

            return Result.Ok(decision);
        }
    }
}
