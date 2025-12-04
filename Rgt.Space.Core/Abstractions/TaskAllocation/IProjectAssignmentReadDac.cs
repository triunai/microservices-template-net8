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

    /// <summary>
    /// Retrieves a paginated matrix of projects and their assignments.
    /// </summary>
    Task<(IReadOnlyList<ProjectAssignmentReadModel> Items, int TotalCount)> GetMatrixAsync(
        int page, 
        int pageSize, 
        Guid? clientId, 
        string? search, 
        CancellationToken ct);
}
