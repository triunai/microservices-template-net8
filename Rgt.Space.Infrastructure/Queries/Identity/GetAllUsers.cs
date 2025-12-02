using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.Identity;
using Rgt.Space.Core.Domain.Contracts.Identity;
using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.Identity;

public class GetAllUsers
{
    public sealed record Query(string? SearchTerm) : IRequest<Result<IReadOnlyList<UserResponse>>>;

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<UserResponse>>>
    {
        private readonly IUserReadDac _dac;

        public Handler(IUserReadDac dac)
        {
            _dac = dac;
        }

        public async Task<Result<IReadOnlyList<UserResponse>>> Handle(Query request, CancellationToken ct)
        {
            IReadOnlyList<UserReadModel> users;

            if (string.IsNullOrWhiteSpace(request.SearchTerm))
            {
                users = await _dac.GetAllAsync(ct);
            }
            else
            {
                users = await _dac.SearchAsync(request.SearchTerm, ct);
            }

            var response = users.Select(u => new UserResponse(
                u.Id,
                u.DisplayName,
                u.Email,
                u.ContactNumber,
                u.IsActive,
                u.CreatedAt,
                u.CreatedBy,
                u.UpdatedAt,
                u.UpdatedBy)).ToList();

            return Result.Ok<IReadOnlyList<UserResponse>>(response);
        }
    }
}
