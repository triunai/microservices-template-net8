# Permanent State — Rgt.Space API

> Persistent tech debt and architectural TODOs. Not session-specific — survives across sessions.
> For session-specific tracking, see `hot-state.md`.

---

## Tech Debt: Pipeline Key Normalization

**Priority:** Medium (code smell, no runtime impact)
**Scope:** All DAC constructors in `Infrastructure/Persistence/Dac/`

### Problem
DACs inconsistently use `"PortalDb"` vs `"System"` pipeline keys despite both hitting the same database (`rgt_space_portal`). The only difference is resilience settings:

| Pipeline Key | Resilience Config Source | Current Users |
|-------------|-------------------------|---------------|
| `"PortalDb"` | `ResilienceSettings.TenantDb` | Portal Routing DACs, Role DACs, Feature Flag DACs |
| `"System"` | `ResilienceSettings.MasterDb` | Some Identity DACs (comment says Identity, but code varies) |

Both pipelines connect to the same `PortalDb` connection string via `ISystemConnectionFactory`.

### What Needs to Happen
1. Audit every DAC constructor to see which pipeline key it actually uses
2. Normalize all to `"PortalDb"` (the canonical name for the single database)
3. Remove `"System"` pipeline registration from `Extensions.cs` (or alias it to `"PortalDb"` settings)
4. Update `Infrastructure/CLAUDE.md` to reflect the single pipeline

### Why Not Now
- Zero runtime impact (both pipelines work fine)
- Changing pipeline keys could affect resilience behavior if settings differ
- Need to verify `ResilienceSettings.MasterDb` vs `ResilienceSettings.TenantDb` values are identical or merge them

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

## Tech Debt: Temporary Auth Bypasses on Feature Flag Endpoints

**Priority:** High (MUST restore before merge/deploy)
**Scope:** 8 endpoint files + `Extensions.cs`

### Problem
For Swagger testing, auth was temporarily disabled:
- 8 feature flag endpoints: `Permissions(...)` → `AllowAnonymous()`
- `Extensions.cs` line ~228: `CurrentUser` → `DevCurrentUser`

All locations marked with `// TODO: Restore auth after Swagger testing`.

### How to Restore
```bash
# Find all affected files
grep -r "TODO: Restore auth after Swagger testing" --include="*.cs" -l
# OR
grep -r "TODO: Restore CurrentUser after Swagger testing" --include="*.cs" -l
```

### When to Restore
After Swagger testing is complete and integration tests are written (integration tests handle auth differently via `CustomWebApplicationFactory`).

---

## Tech Debt: Inherited from Root CLAUDE.md

1. **Tenant header spoofing** — `X-Tenant` not validated against JWT claim
2. **TenantResolutionMiddleware order** — JWT `tid` check before `UseAuthentication()` (dead code)
3. **RequireHttpsMetadata = false** — SSO metadata discovery not enforcing HTTPS
4. **CORS AllowAll** — needs production restriction
5. **FluentAssertions v6 pin** — v8 requires commercial license
