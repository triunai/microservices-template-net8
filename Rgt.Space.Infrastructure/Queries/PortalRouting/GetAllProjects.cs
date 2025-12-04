using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.PortalRouting;
using Rgt.Space.Core.Domain.Contracts.PortalRouting;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.Infrastructure.Queries.PortalRouting;

public class GetAllProjects
{
    // 1) Query contract
    public sealed record Query() : IRequest<Result<IReadOnlyList<ProjectResponse>>>;

    // 2) Validator
    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            // No parameters to validate
        }
    }

    // 3) Handler
    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<ProjectResponse>>>
    {
        private readonly IProjectReadDac _dac;
        private readonly PortalRoutingMapper _mapper;

        public Handler(IProjectReadDac dac, PortalRoutingMapper mapper)
        {
            _dac = dac;
            _mapper = mapper;
        }

        public async Task<Result<IReadOnlyList<ProjectResponse>>> Handle(Query q, CancellationToken ct)
        {
            // Validate inline
            var v = new Validator();
            var vr = await v.ValidateAsync(q, ct);
            if (!vr.IsValid)
                return Result.Fail<IReadOnlyList<ProjectResponse>>(
                    vr.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR").ToArray());

            // Fetch from DAC
            var data = await _dac.GetAllAsync(ct);

            // Map to response
            var response = data.Select(_mapper.ToResponse).ToList();

            return Result.Ok<IReadOnlyList<ProjectResponse>>(response);
        }
    }
}
