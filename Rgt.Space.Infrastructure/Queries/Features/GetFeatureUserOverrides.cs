using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class GetFeatureUserOverrides
{
    public sealed record Query(Guid FeatureId) : IRequest<Result<IReadOnlyList<UserOverrideDetailReadModel>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<UserOverrideDetailReadModel>>>
    {
        private readonly IFeatureReadDac _dac;

        public Handler(IFeatureReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<UserOverrideDetailReadModel>>> Handle(Query request, CancellationToken ct)
        {
            var overrides = await _dac.GetUserOverridesByFeatureAsync(request.FeatureId, ct);
            return Result.Ok(overrides);
        }
    }
}
