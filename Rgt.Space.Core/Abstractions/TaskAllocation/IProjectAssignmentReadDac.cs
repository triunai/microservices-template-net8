using Rgt.Space.Core.ReadModels;

namespace Rgt.Space.Core.Abstractions.TaskAllocation;

public interface IProjectAssignmentReadDac
{
    /// <summary>
    /// Gets all project assignments (Flat List)
    /// </summary>
    Task<IReadOnlyList<ProjectAssignmentReadModel>> GetAllAsync(CancellationToken ct);
    
    /// <summary>
    /// Gets assignments for a specific project
    /// </summary>
    Task<IReadOnlyList<ProjectAssignmentReadModel>> GetByProjectIdAsync(Guid projectId, CancellationToken ct);
}
