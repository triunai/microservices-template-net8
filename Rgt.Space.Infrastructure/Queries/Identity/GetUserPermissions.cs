using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;

namespace Rgt.Space.Infrastructure.Queries.Identity;

public class GetUserPermissions
{
    public sealed record Query(Guid UserId) : IRequest<Result<IReadOnlyList<UserPermissionResponse>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<UserPermissionResponse>>>
    {
        private readonly IUserReadDac _dac;

        public Handler(IUserReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<UserPermissionResponse>>> Handle(Query request, CancellationToken ct)
        {
            var permissions = await _dac.GetPermissionsAsync(request.UserId, ct);

            var response = permissions.Select(p => new UserPermissionResponse(
                p.Module,
                p.SubModule,
                p.CanView,
                p.CanInsert,
                p.CanEdit,
                p.CanDelete)).ToList();

            return Result.Ok<IReadOnlyList<UserPermissionResponse>>(response);
        }
    }
}
