using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Features;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Features;

public static class GetUserOverrides
{
    public sealed record Query(Guid UserId) : IRequest<Result<IReadOnlyList<UserOverrideByUserReadModel>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<UserOverrideByUserReadModel>>>
    {
        private readonly IFeatureReadDac _dac;

        public Handler(IFeatureReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<UserOverrideByUserReadModel>>> Handle(Query request, CancellationToken ct)
        {
            var overrides = await _dac.GetOverridesByUserAsync(request.UserId, ct);
            return Result.Ok(overrides);
        }
    }
}
