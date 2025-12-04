using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Identity;

public static class GetRoleById
{
    public record Query(Guid RoleId) : IRequest<Result<RoleReadModel>>;

    public class Handler : IRequestHandler<Query, Result<RoleReadModel>>
    {
        private readonly IRoleReadDac _roleReadDac;

        public Handler(IRoleReadDac roleReadDac)
        {
            _roleReadDac = roleReadDac;
        }

        public async Task<Result<RoleReadModel>> Handle(Query request, CancellationToken ct)
        {
            var role = await _roleReadDac.GetByIdAsync(request.RoleId, ct);
            
            if (role is null)
            {
                return Result.Fail(ErrorCatalog.ROLE_NOT_FOUND);
            }

            return Result.Ok(role);
        }
    }
}
