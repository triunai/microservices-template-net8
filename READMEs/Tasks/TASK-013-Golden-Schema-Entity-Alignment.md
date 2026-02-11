# TASK-013: Golden Schema + Entity Alignment

## Objective
Create a canonical golden schema (one file per table + extracted functions) and fix all entity classes to match their SQL tables.

## Scope

### Phase A: Golden Schema Extraction
- Rewrite all 19 table `.sql` files in `READMEs/SQL/PostgreSQL/Tables/` to reflect final state after all migrations
- Create `Functions/` folder with extracted trigger and UUID functions
- Delete 3 junk files: `actions copy.sql`, `UAM-Schema.sql`, `UAM-Tenantless.sql`
- Each file has standardized header with migration history, business rules, indexes, triggers

### Phase B: Entity Alignment
Fix 11 entity mismatches:
| Entity | Fix |
|--------|-----|
| Role | Add `Code`, `IsActive` properties |
| Resource | Remove phantom `SortOrder` property |
| Module | Add `IsActive` property |
| UserPermissionOverride | Add `Reason` property |
| Action | TODO: SQL has no soft-delete but entity inherits AuditableEntity |
| Permission | TODO: SQL has no soft-delete but entity inherits AuditableEntity |
| RolePermission | TODO: junction table mismatch |
| UserRole | TODO: assigned_by vs created_by |

### Phase C: Convention Cleanup
- Fix `Guid.NewGuid()` → `Uuid7.NewUuid7()` in Entity.cs base class and all entity Create() methods
- Build + test verification

## Success Criteria
- 19 golden schema files, 0 stale
- Functions/ folder with trigger + UUID definitions
- All entity properties match SQL columns
- `dotnet build` — 0 errors, 0 warnings
- `dotnet test` — 146/146 green
