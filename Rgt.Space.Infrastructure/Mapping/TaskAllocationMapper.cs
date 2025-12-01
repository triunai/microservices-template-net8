using Riok.Mapperly.Abstractions;
using Rgt.Space.Core.ReadModels;
using Rgt.Space.Core.Domain.Contracts.TaskAllocation;

namespace Rgt.Space.Infrastructure.Mapping;

[Mapper]
public partial class TaskAllocationMapper
{
    public partial ProjectAssignmentResponse ToResponse(ProjectAssignmentReadModel source);
    public partial IReadOnlyList<ProjectAssignmentResponse> ToResponseList(IReadOnlyList<ProjectAssignmentReadModel> source);
}
