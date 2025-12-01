using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.Infrastructure.Queries.PortalRouting;

public class GetProjectsByClient
{
    // 1) Query contract
    public sealed record GetProjectsByClientQuery(Guid ClientId) : IRequest<Result<IReadOnlyList<ProjectResponse>>>;

    // 2) Validator
    public sealed class Validator : AbstractValidator<GetProjectsByClientQuery>
    {
        public Validator()
        {
            RuleFor(x => x.ClientId)
                .NotEqual(Guid.Empty)
                .WithErrorCode("CLIENT_ID_INVALID")
                .WithMessage("Client ID cannot be empty");
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<GetProjectsByClientQuery, Result<IReadOnlyList<ProjectResponse>>>
    {
        private readonly IProjectReadDac _dac;
        private readonly IClientReadDac _clientDac;
        private readonly PortalRoutingMapper _mapper;

        public Handler(IProjectReadDac dac, IClientReadDac clientDac, PortalRoutingMapper mapper)
        {
            _dac = dac;
            _clientDac = clientDac;
            _mapper = mapper;
        }

        public async Task<Result<IReadOnlyList<ProjectResponse>>> Handle(GetProjectsByClientQuery q, CancellationToken ct)
        {
            // Validate inline
            var v = new Validator();
            var vr = await v.ValidateAsync(q, ct);
            if (!vr.IsValid)
                return Result.Fail<IReadOnlyList<ProjectResponse>>(
                    vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToArray());

            // Business rule: Check if client exists
            var client = await _clientDac.GetByIdAsync(q.ClientId, ct);
            if (client is null)
                return Result.Fail<IReadOnlyList<ProjectResponse>>("CLIENT_NOT_FOUND");

            // Fetch projects for this client
            var data = await _dac.GetByClientIdAsync(q.ClientId, ct);

            // Map to response
            var response = data.Select(_mapper.ToResponse).ToList();

            return Result.Ok<IReadOnlyList<ProjectResponse>>(response);
        }
    }
}
