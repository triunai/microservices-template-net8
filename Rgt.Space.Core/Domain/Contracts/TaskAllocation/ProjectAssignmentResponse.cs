namespace Rgt.Space.Core.Domain.Contracts.TaskAllocation;

public sealed record ProjectAssignmentResponse(
    Guid ProjectId,
    string ProjectName,
    string ProjectCode,
    Guid ClientId,
    string ClientName,
    Guid UserId,
    string UserName,
    string PositionCode
);
