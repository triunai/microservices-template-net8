using FluentResults;
using FluentValidation;
using MediatR;
using Rgt.Space.Core.Abstractions.TaskAllocation;
using Rgt.Space.Core.Domain.Contracts.TaskAllocation;
using Rgt.Space.Infrastructure.Mapping;

namespace Rgt.Space.Infrastructure.Queries.TaskAllocation;

public class GetStaffingMatrix
{
    public sealed record Query(
        int Page,
        int PageSize,
        Guid? ClientId,
        string? Search
    ) : IRequest<Result<StaffingMatrixResponse>>;

    public sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(x => x.Page)
                .GreaterThan(0)
                .WithMessage("Page must be greater than 0");

            RuleFor(x => x.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100");
        }
    }

    public sealed class Handler : IRequestHandler<Query, Result<StaffingMatrixResponse>>
    {
        private readonly IProjectAssignmentReadDac _dac;
        private readonly TaskAllocationMapper _mapper;

        public Handler(IProjectAssignmentReadDac dac, TaskAllocationMapper mapper)
        {
            _dac = dac;
            _mapper = mapper;
        }

        public async Task<Result<StaffingMatrixResponse>> Handle(Query request, CancellationToken ct)
        {
            var validator = new Validator();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                return Result.Fail<StaffingMatrixResponse>(
                    validationResult.Errors.Select(e => e.ErrorCode ?? "VALIDATION_ERROR"));
            }

            var (items, totalCount) = await _dac.GetMatrixAsync(
                request.Page,
                request.PageSize,
                request.ClientId,
                request.Search,
                ct);

            var response = _mapper.ToMatrixResponse(items, totalCount, request.Page, request.PageSize);
            
            return Result.Ok(response);
        }
    }
}
