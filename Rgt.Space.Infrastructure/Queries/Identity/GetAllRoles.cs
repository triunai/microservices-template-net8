using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Identity;

public static class GetAllRoles
{
    public record Query() : IRequest<Result<IReadOnlyList<RoleReadModel>>>;

    public class Handler : IRequestHandler<Query, Result<IReadOnlyList<RoleReadModel>>>
    {
        private readonly IRoleReadDac _roleReadDac;

        public Handler(IRoleReadDac roleReadDac)
        {
            _roleReadDac = roleReadDac;
        }

        public async Task<Result<IReadOnlyList<RoleReadModel>>> Handle(Query request, CancellationToken ct)
        {
            var roles = await _roleReadDac.GetAllAsync(ct);
            return Result.Ok(roles);
        }
    }
}
