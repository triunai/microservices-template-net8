using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.Infrastructure.Queries.PortalRouting;

public class GetAllClients
{
    // 1) Query contract
    public sealed record GetAllClientsQuery() : IRequest<Result<IReadOnlyList<ClientResponse>>>;

    // 2) Validator (no validation needed for parameterless query)
    public sealed class Validator : AbstractValidator<GetAllClientsQuery>
    {
        public Validator()
        {
            // No parameters to validate
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<GetAllClientsQuery, Result<IReadOnlyList<ClientResponse>>>
    {
        private readonly IClientReadDac _dac;
        private readonly PortalRoutingMapper _mapper;

        public Handler(IClientReadDac dac, PortalRoutingMapper mapper)
        {
            _dac = dac;
            _mapper = mapper;
        }

        public async Task<Result<IReadOnlyList<ClientResponse>>> Handle(GetAllClientsQuery q, CancellationToken ct)
        {
            // Validate inline
            var v = new Validator();
            var vr = await v.ValidateAsync(q, ct);
            if (!vr.IsValid)
                return Result.Fail<IReadOnlyList<ClientResponse>>(
                    vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToArray());

            // Fetch from DAC
            var data = await _dac.GetAllAsync(ct);

            // Map to response
            var response = data.Select(_mapper.ToResponse).ToList();

            return Result.Ok<IReadOnlyList<ClientResponse>>(response);
        }
    }
}
