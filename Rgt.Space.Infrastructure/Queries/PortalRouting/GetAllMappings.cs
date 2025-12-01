using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.Infrastructure.Queries.PortalRouting;

public class GetAllMappings
{
    // 1) Query contract
    public sealed record GetAllMappingsQuery() : IRequest<Result<IReadOnlyList<ClientProjectMappingResponse>>>;

    // 2) Validator
    public sealed class Validator : AbstractValidator<GetAllMappingsQuery>
    {
        public Validator()
        {
            // No parameters to validate
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<GetAllMappingsQuery, Result<IReadOnlyList<ClientProjectMappingResponse>>>
    {
        private readonly IClientProjectMappingReadDac _dac;
        private readonly PortalRoutingMapper _mapper;

        public Handler(IClientProjectMappingReadDac dac, PortalRoutingMapper mapper)
        {
            _dac = dac;
            _mapper = mapper;
        }

        public async Task<Result<IReadOnlyList<ClientProjectMappingResponse>>> Handle(GetAllMappingsQuery q, CancellationToken ct)
        {
            // Fetch all mappings (Admin Console view)
            var data = await _dac.GetAllAsync(ct);

            // Map to response 
            var response = data.Select(_mapper.ToResponse).ToList();

            return Result.Ok<IReadOnlyList<ClientProjectMappingResponse>>(response);
        }
    }
}
