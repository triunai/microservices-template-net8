using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Infrastructure.Services.Features;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class BulkEvaluateFeatures
{
    public sealed record Query(
        Guid UserId,
        Guid? ClientId) : IRequest<Result<Response>>;

    public sealed record Response(
        Guid UserId,
        Guid? ClientId,
        DateTime EvaluatedAt,
        IReadOnlyList<FeatureDecision> Features);

    public sealed class Handler : IRequestHandler<Query, Result<Response>>
    {
        private readonly IFeatureReadDac _readDac;

        public Handler(IFeatureReadDac readDac)
        {
            _readDac = readDac;
        }

        public async Task<Result<Response>> Handle(Query request, CancellationToken ct)
        {
            // 3 bulk queries — always fresh (no cache)
            var features = await _readDac.GetAllAsync(ct);

            var clientSubs = request.ClientId.HasValue && request.ClientId.Value != Guid.Empty
                ? await _readDac.GetClientFeaturesByClientAsync(request.ClientId.Value, ct)
                : [];

            var userOverrides = await _readDac.GetUserOverridesByUserAsync(request.UserId, ct);

            // Build dictionaries for O(1) lookup per feature
            var clientSubDict = clientSubs.ToDictionary(cf => cf.FeatureId);
            var overrideDict = userOverrides.ToDictionary(uo => uo.FeatureId);

            var clientId = request.ClientId ?? Guid.Empty;
            var decisions = new List<FeatureDecision>(features.Count);

            foreach (var feature in features)
            {
                clientSubDict.TryGetValue(feature.Id, out var clientFeature);
                overrideDict.TryGetValue(feature.Id, out var userOverride);

                var decision = FeatureEvaluator.Evaluate(
                    feature, feature.Code, clientId, clientFeature, userOverride);
                decisions.Add(decision);
            }

            return Result.Ok(new Response(
                request.UserId,
                request.ClientId,
                DateTime.UtcNow,
                decisions));
        }
    }
}
