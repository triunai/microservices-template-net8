using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class GetAllFeatures
{
    public sealed record Query : IRequest<Result<IReadOnlyList<FeatureReadModel>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<FeatureReadModel>>>
    {
        private readonly IFeatureReadDac _dac;

        public Handler(IFeatureReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<FeatureReadModel>>> Handle(Query request, CancellationToken ct)
        {
            var features = await _dac.GetAllAsync(ct);
            return Result.Ok(features);
        }
    }
}
