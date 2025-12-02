namespace Rgt.Space.Core.ReadModels;

public record UserPermissionReadModel(
    string Module,
    string SubModule,
    bool CanView,
    bool CanInsert,
    bool CanEdit,
    bool CanDelete
);
