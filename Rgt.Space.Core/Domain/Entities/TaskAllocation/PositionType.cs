using Rgt.Space.Core.Constants;
using Rgt.Space.Core.Domain.Primitives;

namespace Rgt.Space.Core.Domain.Entities.TaskAllocation;

public sealed class PositionType
{
    public string Code { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public string Status { get; private set; } = StatusConstants.Active;
    
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private PositionType() { }

    public static PositionType Create(string code, string name, int sortOrder, string? description = null, string status = StatusConstants.Active)
    {
        if (sortOrder < 0) throw new ArgumentException("SortOrder cannot be negative", nameof(sortOrder));

        return new PositionType
        {
            Code = code,
            Name = name,
            SortOrder = sortOrder,
            Description = description,
            Status = status,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }
}
