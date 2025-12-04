using FastEndpoints;
using MediatR;
using Rgt.Space.API.ProblemDetails;
using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Contracts.TaskAllocation;
using TaskAllocationQueries = Rgt.Space.Infrastructure.Queries.TaskAllocation;

namespace Rgt.Space.API.Endpoints.TaskAllocation.GetStaffingMatrix;

public class GetStaffingMatrixRequest
{
    [QueryParam] public int Page { get; set; } = 1;
    [QueryParam] public int PageSize { get; set; } = 20;
    [QueryParam] public Guid? ClientId { get; set; }
    [QueryParam] public string? Search { get; set; }
}

public sealed class Endpoint(IMediator mediator) : Endpoint<GetStaffingMatrixRequest, StaffingMatrixResponse>
{
    public override void Configure()
    {
        Get("/api/v1/assignments");
        
        // Security: Require VIEW permission on MEMBERS_DIST
        Permissions(
            $"{TaskAllocationConstants.Modules.TaskAllocation}.{TaskAllocationConstants.SubModules.MembersDist}.{TaskAllocationConstants.Actions.View}"
        );

        Summary(s =>
        {
            s.Summary = "Get Staffing Matrix";
            s.Description = "Retrieves a paginated list of projects and their assignments (God View).";
            s.Response<StaffingMatrixResponse>(200, "Matrix returned successfully");
        });
    }

    public override async Task HandleAsync(GetStaffingMatrixRequest req, CancellationToken ct)
    {
        var query = new TaskAllocationQueries.GetStaffingMatrix.Query(
            req.Page,
            req.PageSize,
            req.ClientId,
            req.Search
        );

        var res = await mediator.Send(query, ct);

        if (res.IsFailed)
        {
            var problemDetails = res.ToProblemDetails(HttpContext);
            // Use HttpContext extension to bypass strict type checking of Endpoint<TReq, TRes>
            await HttpContext.Response.SendAsync(problemDetails, problemDetails.Status ?? 500, cancellation: ct);
            return;
        }

        await Send.OkAsync(res.Value, ct);
    }
}
