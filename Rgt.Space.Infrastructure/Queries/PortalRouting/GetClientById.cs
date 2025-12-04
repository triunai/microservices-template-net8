using FluentResults;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;

using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Infrastructure.Queries.PortalRouting;

public static class GetClientById
{
    public record Query(Guid Id) : IRequest<Result<ClientReadModel>>;

    public class Handler : IRequestHandler<Query, Result<ClientReadModel>>
    {
        private readonly IClientReadDac _readDac;

        public Handler(IClientReadDac readDac)
        {
            _readDac = readDac;
        }

        public async Task<Result<ClientReadModel>> Handle(Query request, CancellationToken ct)
        {
            var client = await _readDac.GetByIdAsync(request.Id, ct);
            if (client is null)
            {
                return Result.Fail("CLIENT_NOT_FOUND");
            }

            return Result.Ok(client);
        }
    }
}
