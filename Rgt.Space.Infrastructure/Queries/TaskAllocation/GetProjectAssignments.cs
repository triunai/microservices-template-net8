using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.TaskAllocation;
using Rgt.Space.Core.Domain.Contracts.TaskAllocation;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.Infrastructure.Queries.TaskAllocation;

public class GetProjectAssignments
{
    public sealed record Query(Guid ProjectId) : IRequest<Result<IReadOnlyList<ProjectAssignmentResponse>>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.ProjectId)
                .NotEmpty()
                .WithErrorCode("PROJECT_ID_REQUIRED")
                .WithMessage("Project ID is required");
        }
    }

    public sealed class Handler : IRequestHandler<Query, Result<IReadOnlyList<ProjectAssignmentResponse>>>
    {
        private readonly IProjectAssignmentReadDac _dac;
        private readonly TaskAllocationMapper _mapper;

        public Handler(IProjectAssignmentReadDac dac, TaskAllocationMapper mapper)
        {
            _dac = dac;
            _mapper = mapper;
        }

        public async Task<Result<IReadOnlyList<ProjectAssignmentResponse>>> Handle(Query request, CancellationToken ct)
        {
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Result.Fail<IReadOnlyList<ProjectAssignmentResponse>>(
                    validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR"));
            }

            var data = await _dac.GetByProjectIdAsync(request.ProjectId, ct);
            var response = _mapper.ToResponseList(data);
            
            return Result.Ok(response);
        }
    }
}
