using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class GetClientSubscriptions
{
    public sealed record Query(Guid FeatureId) : IRequest<Result<IReadOnlyList<ClientFeatureDetailReadModel>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<ClientFeatureDetailReadModel>>>
    {
        private readonly IFeatureReadDac _dac;

        public Handler(IFeatureReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<ClientFeatureDetailReadModel>>> Handle(Query request, CancellationToken ct)
        {
            var subscriptions = await _dac.GetClientSubscriptionsByFeatureAsync(request.FeatureId, ct);
            return Result.Ok(subscriptions);
        }
    }
}
