namespace Rgt.Space.Core.ReadModels;

/// <summary>
/// Flat read model for a single project assignment.
/// Represents: One User assigned to One Position on One Project.
/// </summary>
public sealed record ProjectAssignmentReadModel(
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    Guid ClientId,
    string ClientName,
    Guid UserId,
    string UserName,
    string PositionCode
);
