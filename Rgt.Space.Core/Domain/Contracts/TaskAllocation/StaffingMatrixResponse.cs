namespace Rgt.Space.Core.Domain.Contracts.TaskAllocation;

public sealed record StaffingMatrixResponse(
    IReadOnlyList<StaffingMatrixProject> Items,
    int TotalCount,
    int Page,
    int PageSize);

public sealed record StaffingMatrixProject(
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    Guid ClientId,
    string ClientName,
    IReadOnlyList<StaffingMatrixAssignment> Assignments);

public sealed record StaffingMatrixAssignment(
    string PositionCode,
    Guid UserId,
    string UserName);
