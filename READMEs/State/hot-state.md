# Hot State — Rgt.Space API

> Living document. Updated each session.

## Current Branch
`test/rbac-positiontype-verification-8034414594985432536`

## Project Health
- **Build**: 0 errors, 0 warnings (verified 2026-02-11)
- **Tests**: 97 unit + 49 integration = 146 total, 0 failures
- **Last commit**: `1c76808` — feat: combo breaker
- **Docker**: Redis via docker-compose, PostgreSQL external (Testcontainers for tests)
- **CI/CD**: GitHub Actions (dependabot configured)
- **Auth**: `CurrentUser` (JWT-based) ACTIVE in production, `DevCurrentUser` only in tests
- **Migration load order**: 00 → 01 → 03 → 01a → 02 → 06 → 08 → 09 → 10 → 11 → 12 → 13

## What's Set Up
- [x] 5x CLAUDE.md files (root, Core, Infrastructure, API, Tests)
- [x] Auto memory (MEMORY.md + hot-state + timeline)
- [x] `.claude/settings.local.json` — allows `dotnet build` and `dir`
- [x] Pre-Implementation Verification Gate added to root CLAUDE.md
- [x] Permanent state doc (`READMEs/State/state.md`) for tech debt tracking
- [x] Full integration test suite (DAC + endpoint tests)

## What's NOT Set Up Yet
- [ ] Hooks (auto-format on edit, block sensitive files, auto-build)
- [ ] MCP servers (context7 for live docs, GitHub MCP)
- [ ] Custom skills (`/gen-test`, `/create-migration`)
- [ ] Subagent definitions (`.claude/agents/security-reviewer.md`)

---

## COMPLETED: Feature Flag System (TASK-009)

All 4 phases + hardening + integration tests complete.

### Phase Summary
1. **Core Primitives (P1)** — 11 files: entities, read models, interfaces, constants
2. **Infrastructure (P2)** — 3 files + SQL migration + DI
3. **Admin Endpoints (P3)** — 4 DTOs + 2 queries + 6 commands + 8 endpoints (22 files)
4. **Unit Tests (P4)** — 9 test files, 41 unit tests, all green
5. **Hardening** — 9 fixes, 4 new tests (71 unit total)
6. **Integration Tests** — 16 DAC tests + 14 endpoint tests (30 new, all green)
7. **Code Review MEDs** — 5 fixes (sealed records, LIMIT, guard removal, user check, ID source)

---

## COMPLETED: TASK-010 — Feature Flag Read Endpoints (2026-02-10)

4 new GET endpoints implemented via parallel swarming (Step 0 + 2 agents):

| Route | Purpose |
|-------|---------|
| `GET /features/{featureId}/clients` | Client subscriptions by feature (JOIN clients) |
| `GET /clients/{clientId}/features` | Features by client (JOIN features) |
| `GET /features/{featureId}/user-overrides` | User overrides by feature (JOIN users) |
| `GET /users/{userId}/feature-overrides` | Overrides by user (JOIN features) |

13 new files, +16 tests (8 unit + 4 DAC integration + 4 endpoint integration).

---

## COMPLETED: TASK-011 — Feature Flag Evaluation Endpoints

**Spec:** `READMEs/Tasks/TASK-011-Feature-Flag-Evaluation-Endpoints.md`

### What's DONE (this session, 2026-02-10)

**Phase 1 — COMPLETE: Extracted FeatureEvaluator + GRANTED split**
- Created `Infrastructure/Services/Features/FeatureEvaluator.cs` — pure static evaluator (single source of truth)
- Refactored `FeatureGate.cs` — delegates to `FeatureEvaluator.Evaluate()`, progressive data fetching with short-circuits
- Split `GRANTED` → `GRANTED_SYSTEM` + `GRANTED_CLIENT` in `FeatureFlagConstants.cs`
- Added `EvaluateView` permission constant
- Updated 4 test assertions in `FeatureGateTests.cs`
- **Verified: 0 errors, 0 warnings, 21/21 FeatureGate tests green**

**Phase 2 (partial) — COMPLETE: Bulk DAC methods added**
- Added `GetClientFeaturesByClientAsync(clientId)` to `IFeatureReadDac` + `FeatureReadDac`
- Added `GetUserOverridesByUserAsync(userId)` to `IFeatureReadDac` + `FeatureReadDac`
- **Verified: builds clean**

**Phase 3 — COMPLETE: Endpoints + handlers built (tests deferred)**
- Created `Infrastructure/Queries/Features/EvaluateFeature.cs` — single eval query + handler (uses IFeatureGate, cached)
- Created `Infrastructure/Queries/Features/BulkEvaluateFeatures.cs` — bulk eval query + handler + Response DTO (3 DAC queries + evaluator loop, fresh)
- Created `API/Endpoints/Features/EvaluateFeature/Endpoint.cs` — `GET /api/v1/features/evaluate/{featureCode}?userId=&clientId=`
- Created `API/Endpoints/Features/BulkEvaluateFeatures/Endpoint.cs` — `GET /api/v1/features/evaluate?userId=&clientId=`
- Both use `AllowAnonymous()` + `// TODO: Restore auth after Swagger testing`
- userId validation: `ThrowIfAnyErrors()` on missing/empty → 400
- **Verified: full solution 0 errors, 0 warnings, 80/80 unit tests green**

### New Files Created (6 total)
| Layer | File | Purpose |
|-------|------|---------|
| Infrastructure | `Services/Features/FeatureEvaluator.cs` | Pure static decision tree |
| Infrastructure | `Queries/Features/EvaluateFeature.cs` | Single eval query + handler |
| Infrastructure | `Queries/Features/BulkEvaluateFeatures.cs` | Bulk eval query + handler + Response DTO |
| API | `Endpoints/Features/EvaluateFeature/Endpoint.cs` | Single eval HTTP endpoint |
| API | `Endpoints/Features/BulkEvaluateFeatures/Endpoint.cs` | Bulk eval HTTP endpoint |

### Files Modified (4 total)
| File | Change |
|------|--------|
| `Core/Constants/FeatureFlagConstants.cs` | Added `GrantedSystem`, `GrantedClient`, `EvaluateView` |
| `Core/Abstractions/Features/IFeatureReadDac.cs` | Added 2 bulk methods |
| `Infrastructure/Persistence/Dac/Features/FeatureReadDac.cs` | Added 2 bulk method implementations |
| `Infrastructure/Services/Features/FeatureGate.cs` | Delegates to FeatureEvaluator, GRANTED_SYSTEM in global path |
| `Tests/Unit/Services/FeatureGateTests.cs` | 4 assertions: `Granted` → `GrantedSystem`/`GrantedClient` |

### Tests — COMPLETE (2026-02-10, session 2)

**+23 new tests** written via parallel swarming (Agent A: unit, Agent B: integration):

| Test File | Type | Tests | What |
|-----------|------|-------|------|
| `Unit/Services/FeatureEvaluatorTests.cs` | Unit | 10 | All 9 reason codes + truth table row 9 |
| `Unit/Handlers/Features/EvaluateFeatureHandlerTests.cs` | Unit | 3 | IFeatureGate mock, clientId null→Empty |
| `Unit/Handlers/Features/BulkEvaluateFeaturesHandlerTests.cs` | Unit | 4 | IFeatureReadDac mock, skip client fetch |
| `Integration/Persistence/FeatureDacIntegrationTests.cs` | Integration | +2 | Bulk client features + user overrides |
| `Integration/Api/FeatureEndpointTests.cs` | Integration | +4 | Single eval + bulk eval + 400 validation |

Totals: 97 unit + 49 integration = **146 tests, all green**

### Verification Agent Results (nansen)
- **No CRITICAL findings** — spec is sound
- Phase 1 was flagged as "stale" since we completed it during implementation (expected)
- Pattern conformance: all COMPLIANT (DAC, query/handler, endpoint patterns match)
- Logic verified: truth table row 9, GRANTED split, null handling all correct
- Scope calibration: APPROPRIATE

---

## Endpoint Contract (for frontend)

**Single eval:**
```
GET /api/v1/features/evaluate/{featureCode}?userId={guid}&clientId={guid}
→ 200: { "featureCode": "PORTAL_ROUTING", "isEnabled": true, "reasonCode": "GRANTED_CLIENT" }
→ 400: if userId missing/empty
```

**Bulk eval:**
```
GET /api/v1/features/evaluate?userId={guid}&clientId={guid}
→ 200: { "userId": "...", "clientId": "...", "evaluatedAt": "...", "features": [...] }
→ 400: if userId missing/empty
```

- `clientId` optional — system features work without it, client-scoped get `INVALID_CLIENT_ID`
- 200 for ALL evaluations (even `FEATURE_NOT_FOUND`)
- Both under Swagger tag "Feature Flags"

---

## COMPLETED: Schema Audit + Phase 1 Fix (2026-02-10)

### Schema Audit (5-agent swarm)
- 5 agents (3 Opus, 2 Sonnet) produced 6 docs in `READMEs/SQL/PostgreSQL/`
- Master doc: `SCHEMA-AUDIT-MASTER.md` — single source of truth
- Key finding: "Two-Era Schema" — Era 1 (IAM, migration 01) vs Era 2 (Routing + Flags, migrations 03/09)
- 37 FK-constrained audit columns across 13 tables — decision: KEEP (Dapper safety net)
- 6 CRITICAL, 8 HIGH, 11 entity/SQL mismatches, dead code identified

### Phase 1: Unblock Tests — DONE
- Fixed `TestDatabaseInitializer` load order: `00 → 01 → 03 → 01a → 02 → 06 → 08 → 09 → 10`
- Created `01a-seed-devadmin.sql` — seeds DevAdmin with hardcoded ID before migration 02
- Fixed `10-feature-flag-seed.sql` — uses `CROSS JOIN users WHERE email` for audit columns
- **146/146 tests green**

---

## COMPLETED: TASK-012 — Schema Retrofit + Auth Restore (2026-02-11)

### Phase B: Schema Retrofit — DONE
Brought Era 1 tables (migration 01) up to Era 2 standards via 2 new migrations:

**Migration 11 — FK indexes** (`11-add-missing-fk-indexes.sql`):
- 6 indexes on auth/feature-flag hot paths (user_permission_overrides, permissions, resources, user_feature_overrides)

**Migration 12 — Era 1 retrofit** (`12-era1-retrofit.sql`):
- Part 1: 4 zombie-safe partial index conversions (users.email, users.sso, modules.code, resources.module_id+code)
- Part 2: 9 `updated_at` triggers (users, modules, resources, actions, permissions, roles, user_permission_overrides, features, client_features)
- Part 3: 13 timestamp default standardizations (`now()` → `(now() AT TIME ZONE 'utc')`)

**DAC fix**: `ClientProjectMappingWriteDac.cs` — 4 bare `now()` → `(NOW() AT TIME ZONE 'utc')`

**Test fix**: 4 `ON CONFLICT` clauses needed `WHERE is_deleted = FALSE` to match new partial indexes (FeatureEndpointTests, FeatureDacIntegrationTests, UserDacIntegrationTests)

**Migration load order**: `00 → 01 → 03 → 01a → 02 → 06 → 08 → 09 → 10 → 11 → 12`

### Phase A: Auth Restore — DONE
Restored real JWT auth on all feature flag endpoints:

- **Extensions.cs**: Swapped `DevCurrentUser` → `CurrentUser` (JWT-based)
- **12 endpoints**: `AllowAnonymous()` → `Permissions(FeatureFlagConstants.Permissions.XXX)`
- **2 eval endpoints**: Kept `AllowAnonymous()` (frontend needs pre-auth), removed TODO comments
- **TestAuthHandler**: New test auth handler auto-authenticates as DevAdmin with all feature flag permissions
- **Test factories**: Both FeatureEndpointTests + ClientEndpointTests override `ICurrentUser` → `DevCurrentUser` + use TestAuthHandler with `PostConfigure<AuthenticationOptions>`

**Permission mapping**:
| Endpoint | Permission |
|----------|-----------|
| GetFeatures, GetFeatureById, GetClientFeatures, GetClientSubscriptions, GetFeatureUserOverrides, GetUserOverrides | `FEATURES.LIST.VIEW` |
| CreateFeature, UpdateFeature, DeleteFeature | `FEATURES.GLOBAL.EDIT` |
| UpsertClientFeature | `FEATURES.CLIENT.EDIT` |
| SetUserOverride | `FEATURES.OVERRIDE.INSERT` |
| ClearUserOverride | `FEATURES.OVERRIDE.DELETE` |
| EvaluateFeature, BulkEvaluateFeatures | `AllowAnonymous` |

**146/146 tests green, 0 build errors, 0 warnings**

### Migration 13 + Deep Audit — DONE
- Created `13-seed-admin-accounts.sql`: FEATURES RBAC module + admin accounts + SYS_ADMIN permissions
- **Key discovery**: SYS_ADMIN role had ZERO role_permissions before migration 13 — migration 02 created the role, migration 06 generated permissions, but nobody linked them
- Migration 13 Part 5 grants ALL permissions for ALL modules (PORTAL_ROUTING, TASK_ALLOCATION, USER_MGMT, FEATURES) to SYS_ADMIN
- Fixed latent bug in `GetPermissionsAsync`: added `is_deleted = FALSE` filter on modules/resources JOINs
- Deep audit (56-tool agent): 0 critical, 6 warnings, 7 notes, 22 verified — full report at `READMEs/State/MIGRATION-13-AUDIT.md`
- Admin accounts seeded (all SSO): khumeren@gmail.com, khumeren@rgtech.com.my, kent.tan@rgtech.com.my
- E2E testing confirmed working — feature flags visible and manipulable via frontend

---

## NEXT UP

### Priority 1: Compact + Next Session Planning
- E2E testing DONE — feature flags working via frontend with real SSO auth
- 16 non-feature endpoints need `Permissions()` calls (phased rollout)
- Phase 3: Entity/SQL alignment (11 mismatches, dead code removal)

### Priority 2: Phase 3 — Entity/SQL Alignment (separate session)
1. Fix entity classes to match SQL (Role missing Code, Resource phantom SortOrder, etc.)
2. Remove dead code (UserSession, Tenant entity, unused columns)
3. Migrate `Guid.NewGuid()` → `Uuid7.NewUuid7()` in older entities

### Priority 3: Remaining Items
1. **Tooling setup** — hooks, MCP servers, custom skills
2. **GET /api/v1/users/{userId}/clients** endpoint (if frontend needs scoped client list)

---

## Active Tech Debt
> See `READMEs/State/state.md` for permanent tech debt tracking.
1. Pipeline key normalization (`"PortalDb"` vs `"System"`)
2. Tenant header spoofing (X-Tenant not validated against JWT)
3. TenantResolutionMiddleware ordering (tid check before auth)
4. RequireHttpsMetadata = false (SSO)
5. CORS AllowAll (production risk)
6. FluentAssertions v6 pin (v8 commercial)

## Domains & Endpoint Coverage
| Domain | Endpoints | Status |
|--------|-----------|--------|
| Identity (Users) | 11 | Implemented |
| Roles | 5 | Implemented |
| Auth (Login) | 1 | Implemented |
| Portal Routing (Clients) | 5 | Implemented |
| Portal Routing (Projects) | 6 | Implemented |
| Portal Routing (Mappings) | 4 | Implemented |
| Task Allocation | 5 | Implemented |
| Dashboard | 1 | Implemented |
| Health | 4 | Implemented |
| Sales | 1 | Implemented |
| Audit | 1 | Implemented |
| Debug (ComboBreak) | 3 | Dev-only |
| **Feature Flags (Admin)** | **12** | **TASK-009 + TASK-010 COMPLETE** |
| **Feature Flags (Eval)** | **2** | **TASK-011 COMPLETE** |

## Test Coverage
| Category | Files | Tests | Status |
|----------|-------|-------|--------|
| Unit/Entities | 2 | 8 | PositionType, User |
| Unit/Validators | 1 | 3 | ClientValidator |
| Unit/Services | 3 | 34 | IdentitySyncService (3), FeatureGate (21), **FeatureEvaluator (10)** |
| Unit/Handlers/Features | 9 | 25 | All 8 CRUD handlers + TOCTOU + cache + user check |
| Unit/Handlers/Features (reads) | 1 | 8 | 4 read endpoint handlers (2 tests each) |
| Unit/Handlers/Features (eval) | 2 | 7 | **EvaluateFeature (3), BulkEvaluateFeatures (4)** |
| Unit subtotal | 18 | 97 | |
| Integration/API | 2 | 23 | ClientEndpoints (1), **FeatureEndpoints (22)** |
| Integration/Persistence | 3 | 26 | PositionType (1), UserDac (3), **FeatureDac (22)** |
| Integration subtotal | 5 | 49 | **Docker required** |
| **TOTAL** | **23** | **146** | **All green** |
