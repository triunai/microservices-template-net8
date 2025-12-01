using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.TaskAllocation;

public sealed class ProjectAssignment : AuditableEntity
{
    public Guid ProjectId { get; private set; }
    public Guid UserId { get; private set; }
    public string PositionCode { get; private set; } = string.Empty;

    private ProjectAssignment(Guid id) : base(id) { }

    public static ProjectAssignment Create(Guid projectId, Guid userId, string positionCode)
    {
        return new ProjectAssignment(Guid.NewGuid())
        {
            ProjectId = projectId,
            UserId = userId,
            PositionCode = positionCode,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
