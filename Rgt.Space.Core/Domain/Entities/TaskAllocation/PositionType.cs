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

    /// <summary>
    /// Creates a new <see cref="PositionType"/>.
    /// </summary>
    /// <param name="code">The unique code for the position type.</param>
    /// <param name="name">The display name.</param>
    /// <param name="sortOrder">The sort order for UI display. Must be non-negative.</param>
    /// <param name="description">Optional description.</param>
    /// <param name="status">The status (Active/Inactive).</param>
    /// <returns>A new <see cref="PositionType"/> instance.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when sortOrder is negative.</exception>
    public static PositionType Create(string code, string name, int sortOrder, string? description = null, string status = StatusConstants.Active)
    {
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder), "SortOrder cannot be negative");

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
