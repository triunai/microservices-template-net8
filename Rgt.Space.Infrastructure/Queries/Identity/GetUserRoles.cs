using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Errors;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Identity;

public static class GetUserRoles
{
    public record Query(Guid UserId) : IRequest<Result<IReadOnlyList<UserRoleReadModel>>>;

    public class Handler : IRequestHandler<Query, Result<IReadOnlyList<UserRoleReadModel>>>
    {
        private readonly IUserReadDac _userReadDac;
        private readonly IRoleReadDac _roleReadDac;

        public Handler(IUserReadDac userReadDac, IRoleReadDac roleReadDac)
        {
            _userReadDac = userReadDac;
            _roleReadDac = roleReadDac;
        }

        public async Task<Result<IReadOnlyList<UserRoleReadModel>>> Handle(Query request, CancellationToken ct)
        {
            // Check user exists
            var user = await _userReadDac.GetByIdAsync(request.UserId, ct);
            if (user is null)
            {
                return Result.Fail(ErrorCatalog.USER_NOT_FOUND);
            }

            var roles = await _roleReadDac.GetUserRolesAsync(request.UserId, ct);
            return Result.Ok(roles);
        }
    }
}
