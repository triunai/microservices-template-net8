namespace Rgt.Space.Core.Domain.Contracts.Identity;

public record UserPermissionResponse(
    string Module,
    string SubModule,
    bool CanView,
    bool CanInsert,
    bool CanEdit,
    bool CanDelete
);
