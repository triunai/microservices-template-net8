# Permanent State — Rgt.Space API

> Persistent tech debt and architectural TODOs. Not session-specific — survives across sessions.
> For session-specific tracking, see `hot-state.md`.

---

## ~~Tech Debt: Pipeline Key Normalization~~ — RESOLVED (2026-02-12)

**Status:** RESOLVED via TASK-014 Wave 5
**What was done:**
- All 5 DACs normalized from `"System"` → `"PortalDb"`
- Removed `"System"` pipeline registration from `Extensions.cs`
- Updated `Infrastructure/CLAUDE.md` to reflect single pipeline
- 3 test mocks updated

---

## Tech Debt: Feature Flag — Cache Invalidation Window

**Priority:** Low (acceptable for admin-facing system)
**Scope:** `FeatureGate.cs`, all mutation command handlers

### Problem
Between a DB write and the subsequent `_featureGate.Invalidate*()` call, other requests can read stale cached data. This is a natural consequence of "write-then-invalidate" without distributed transactions.

### Why Acceptable
- 60-second cache TTL provides an upper bound on staleness
- Feature flags are admin operations, not high-frequency mutations
- Making this atomic would require cache-aside with distributed locks — massive complexity for near-zero benefit

---

## Tech Debt: Feature Flag — 3 DB Roundtrips per Evaluation

**Priority:** Low (optimization opportunity, not a bug)
**Scope:** `FeatureGate.IsEnabledAsync` → calls up to 3 DAC methods sequentially

### Problem
The waterfall evaluation (feature lookup → client feature → user override) makes up to 3 separate DB connections via 3 DAC methods. Each opens a new NpgsqlConnection from the pool.

### Potential Fix
A single SQL query with LEFT JOINs across all 3 tables could reduce this to 1 roundtrip. However, this would:
1. Violate the DAC interface separation (IFeatureReadDac methods are individually useful)
2. Require a new composite read model
3. Make cache invalidation granularity coarser (one key vs three)

### Why Not Now
- Npgsql connection pooling makes this cheap (~0.1ms per pool checkout)
- Each query is individually cached with 60s TTL, so most requests hit 0 DB calls
- Stampede protection (SemaphoreSlim per key) prevents thundering herd

---

## Tech Debt: Feature Flag — Instance-Local Cache Invalidation

**Priority:** Medium (becomes critical if deploying multiple API nodes)
**Scope:** `IFeatureGate.Invalidate*()` methods

### Problem
Cache invalidation only clears the `IMemoryCache` on the node that handled the admin request. Other nodes serve stale data until the 60-second TTL expires.

### When It Matters
- Single-node deployment (current): no impact
- Multi-node deployment: admin toggles a feature on node A, nodes B and C serve stale state for up to 60 seconds

### Potential Fixes
1. **Redis pub/sub**: Publish invalidation events, all nodes subscribe and clear local cache
2. **Distributed cache**: Replace IMemoryCache with IDistributedCache (Redis), single source of truth
3. **Shorter TTL**: Reduce from 60s to 10-15s (trades freshness for more DB load)

### Why Not Now
- Currently single-node deployment
- Redis infrastructure exists (docker-compose) but is minimally used
- Fix when multi-node is on the roadmap

---

## ~~Tech Debt: Temporary Auth Bypasses on Feature Flag Endpoints~~ — RESOLVED (2026-02-11)

**Status:** RESOLVED via TASK-012 Phase A
**What was done:**
- 12 endpoints: `AllowAnonymous()` → `Permissions(FeatureFlagConstants.Permissions.XXX)`
- 2 eval endpoints: kept `AllowAnonymous()` (frontend pre-auth), TODO comments removed
- `Extensions.cs`: `DevCurrentUser` → `CurrentUser` (JWT-based)
- `TestAuthHandler` created for integration tests (auto-authenticates as DevAdmin with all permissions)
- Both test factories (`FeatureEndpointTests`, `ClientEndpointTests`) override auth scheme + ICurrentUser
- 146/146 tests green

---

## ~~Tech Debt: Two-Era Schema (Schema Audit 2026-02-10)~~ — RESOLVED (2026-02-11)

**Status:** RESOLVED via TASK-012 Phase B (migration 11 + 12)
**What was done:**
- Migration 11: 6 FK indexes on auth/feature-flag hot paths
- Migration 12 Part 1: 4 zombie-safe partial index conversions (users.email, users.sso, modules.code, resources.module_id+code)
- Migration 12 Part 2: 9 `updated_at` triggers for Era 1 tables
- Migration 12 Part 3: 13 timestamp default standardizations
- `ClientProjectMappingWriteDac.cs`: 4 bare `now()` → `(NOW() AT TIME ZONE 'utc')`
- 4 test `ON CONFLICT` clauses fixed to match new partial indexes
- **Remaining (Phase 3, deferred):** Entity/SQL alignment (11 mismatches), dead code removal, Guid.NewGuid → Uuid7 migration
- **H8 (CASCADE vs RESTRICT):** Not addressed — requires broader migration strategy

---

## ~~Tech Debt: Inherited from Root CLAUDE.md~~ — RESOLVED (2026-02-12)

**Status:** RESOLVED via TASK-014 Production Hardening (all 5 waves)
1. ~~Tenant header spoofing~~ — JWT tid validation + mismatch → 403 (Wave 3)
2. ~~TenantResolutionMiddleware order~~ — moved AFTER UseAuthentication (Wave 3)
3. ~~RequireHttpsMetadata = false~~ — gated behind `IsDevelopment()` (Wave 1.2)
4. ~~CORS AllowAll~~ — environment-conditional whitelist (Wave 1.1)
5. **FluentAssertions v6 pin** — v8 requires commercial license (no action needed)

---

## ~~Tech Debt: Production Audit Findings (2026-02-12)~~ — RESOLVED (2026-02-12)

**Status:** RESOLVED via TASK-014 Production Hardening

### ~~CRITICAL~~ — ALL FIXED
- ~~6. Hardcoded JWT signing key~~ — throws on missing, removed from appsettings.json (Wave 1.3)
- ~~7. ShowPII = true unconditional~~ — gated behind `IsDevelopment()` (Wave 1.4)
- ~~8. DB passwords in git-tracked appsettings.json~~ — placeholders + appsettings.Development.json (Wave 1.5)

### ~~HIGH~~ — ALL FIXED
- ~~9. 38 endpoints missing Permissions/AllowAnonymous~~ — full permission rollout (Wave 2)
- ~~10. Debug endpoints in production~~ — env guard returns 404 in non-dev (Wave 4.1)
- ~~11. JWT claims PII logging~~ — claims dump removed, subject at Debug (Wave 4.2)

### ~~MEDIUM~~ — MOSTLY FIXED
- ~~12. Serilog auth Debug in base config~~ — changed to Warning (Wave 1.6)
- Pipeline key normalization: `"System"` → `"PortalDb"` in 5 DACs (Wave 5.1)
- Duplicate route bug in GetProjectAssignments: fixed (Wave 5.4)
- **Deferred:** Health check lockdown, AppException message review (low risk)

---

## ~~SSO Integration Findings (2026-02-12)~~ — IMPLEMENTED (2026-02-12)

**Status:** ALL 9 FIXES IMPLEMENTED + 20 DEDICATED TESTS — 166/166 tests green

### ~~Fixes Required (Portal Side)~~ — ALL DONE
| # | Fix | Severity | Status |
|---|-----|----------|--------|
| 1 | Case-insensitive tenant comparison (`.ToUpperInvariant()`) | CRITICAL | DONE — `TenantResolutionMiddleware.cs` |
| 2 | Use `ext_provider` claim (not `issuer.Contains("localhost")`) | CRITICAL | DONE — `Program.cs` |
| 3 | Remove hardcoded RSA key, use OIDC discovery | WARNING | DONE — `Program.cs` (~28 lines removed) |
| 4 | Remove broker DB connection strings | CLEANUP | DONE — `appsettings.json` + `appsettings.Development.json` |
| 5 | Log `jti` claim for audit traceability | LOW | DONE — `Program.cs` |
| 6 | Remove dead `"per-tenant"` rate limiter policy | CLEANUP | DONE — `Program.cs` |
| 7 | Reject soft-deleted users in JIT sync | CRITICAL | DONE — `IdentitySyncService.cs` (both methods) |
| 8 | Add `tid` to TestAuthHandler | LOW | DONE — `TestAuthHandler.cs` |
| 9 | Remove provider from external ID lookup | MEDIUM | DONE — `IUserReadDac`, `UserReadDac`, `IdentitySyncService`, migration 14, `users.sql` golden schema |

### Decisions — ALL RESOLVED (2026-02-12)
1. **Prod broker URL:** TBD — IP-based (no domain). Dev: `localhost:7012`
2. **Separate domains:** Yes — no cookie sharing, CORS explicit on both sides
3. **RBAC:** Global roles for v1 (portal's decision, broker has no role claims)
4. **Server-to-server:** Not v1 — one-way integration only
5. **JWKS persistence:** Not v1 — coordinate maintenance windows
6. **Soft-deleted re-login:** Reject at portal middleware (defense in depth)
7. **Token contract:** In progress (TOKEN-CONTRACT.md being created on broker side)
8. ~~HandleCallback is_active~~ — **FIXED (2026-02-12)**

### Broker-Side Fixes (Their Responsibility, Affects Us)
- ~~`is_active` not checked in HandleCallback~~ — **FIXED (2026-02-12)**, returns USER_002 / 403
- `is_active` not checked during refresh — **in progress (2026-02-12)**, broker fixing DB proc + C# service layer. New error: `USER_INACTIVE` / 403
- Single-key JWKS with no rotation grace period (pending)
- CORS hardcoded to localhost — needs our prod origins (pending)
- No rate limiting on broker endpoints (pending)
- `expiresIn` hardcoded to 900 in refresh response DTO (pending)
- No API versioning — TOKEN-CONTRACT.md in progress, all 13 claims confirmed stable

### Integration Contract Highlights
- `sub` = broker's user UUID (stable across IdP migrations, shared across tenants)
- `tid` always present, always uppercase in DB, both sides `.ToUpperInvariant()`
- `ext_provider` is free-form string (`Google`, `AzureAD`, `MockIdP`, future values)
- Access token 15 min, refresh token 14 days, one-time rotation with family reuse detection
- Logout revokes specific refresh token only — access token valid until `exp`
- Concurrent multi-tenant sessions fully supported

---

## ~~SSO 401 Bug (2026-02-12)~~ — RESOLVED (2026-02-12)

**Status:** RESOLVED — E2E SSO login working (Google + Microsoft)
**Root Cause:** `Microsoft.IdentityModel.Protocols.OpenIdConnect 7.1.2` (transitive from `JwtBearer 8.0.11`) failed to parse `jwks_uri` from OIDC discovery JSON when other IdentityModel packages were at `8.8.0`. The parser extracted `issuer` but left `JwksUri: null` → `SigningKeys: 0` → `IDX10500`.
**Fix:** Pinned `Microsoft.IdentityModel.Protocols.OpenIdConnect` and `Microsoft.IdentityModel.Protocols` to `8.8.0` in API csproj.
**Secondary fixes (permanent):**
- Auth event logging: `LogDebug` → `LogWarning` (failures) / `LogInformation` (success)
- Serilog bootstrap: now loads `appsettings.{environment}.json` (was only loading base config)
- `BackchannelHttpHandler` with dev SSL bypass (standard ASP.NET Core OIDC pattern)

---

## Remaining Tech Debt

1. **FluentAssertions v6 pin** — v8 requires commercial license (no action needed)
2. **Health check `/health` endpoint** — exposes infra details without auth (low risk for internal APIs)
3. **AppException messages** — may contain table names/SQL in client errors (needs audit)
4. **TrackedEntity base class** — for entities without soft-delete in SQL (no runtime impact)
5. **UpdateUser audit trail** — `updatedBy` uses `req.UserId` not `ICurrentUser.Id` (now that auth is enforced, should use JWT identity)
6. **Debug endpoints AllowAnonymous()** — runtime `IsDevelopment()` guard is correct, but endpoints visible in Swagger across all environments
