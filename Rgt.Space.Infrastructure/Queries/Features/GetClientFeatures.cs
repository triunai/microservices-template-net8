using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class GetClientFeatures
{
    public sealed record Query(Guid ClientId) : IRequest<Result<IReadOnlyList<ClientFeatureByClientReadModel>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<ClientFeatureByClientReadModel>>>
    {
        private readonly IFeatureReadDac _dac;

        public Handler(IFeatureReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<ClientFeatureByClientReadModel>>> Handle(Query request, CancellationToken ct)
        {
            var features = await _dac.GetFeaturesByClientAsync(request.ClientId, ct);
            return Result.Ok(features);
        }
    }
}
