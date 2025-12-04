using Riok.Mapperly.Abstractions;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Core.Domain.Contracts.TaskAllocation;

namespace Rgt.Space.Infrastructure.Mapping;

[Mapper]
public partial class TaskAllocationMapper
{
    public partial ProjectAssignmentResponse ToResponse(ProjectAssignmentReadModel source);
    public partial IReadOnlyList<ProjectAssignmentResponse> ToResponseList(IReadOnlyList<ProjectAssignmentReadModel> source);

    public StaffingMatrixResponse ToMatrixResponse(
        IReadOnlyList<ProjectAssignmentReadModel> flatItems, 
        int totalCount, 
        int page, 
        int pageSize)
    {
        var groupedProjects = flatItems
            .GroupBy(x => x.ProjectId)
            .Select(g => new StaffingMatrixProject(
                g.Key,
                g.First().ProjectName,
                g.First().ProjectCode,
                g.First().ClientId,
                g.First().ClientName,
                g.Where(x => !string.IsNullOrEmpty(x.PositionCode) && x.UserId.HasValue)
                 .Select(x => new StaffingMatrixAssignment(
                     x.PositionCode!,
                     x.UserId!.Value,
                     x.UserName!))
                 .ToList()
            ))
            .ToList();

        return new StaffingMatrixResponse(groupedProjects, totalCount, page, pageSize);
    }
}
