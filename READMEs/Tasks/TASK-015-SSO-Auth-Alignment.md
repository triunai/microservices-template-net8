# TASK-015: SSO Auth Alignment

> Pre-production alignment between `rgt-space-portal` (this API) and `rgt-auth-sso-api` (SSO broker).
> Based on 4 rounds of integration Q&A + 1 alignment check (2026-02-12).

---

## Context

The SSO broker (`rgt-auth-sso-api`) handles OIDC/PKCE flows with identity providers (Google, Azure AD), then mints RS256 JWTs. The portal API validates these JWTs and builds its own RBAC layer on top. Both systems are going to production.

**Broker dev confirmed:** The broker's JWKS/OIDC discovery works on localhost. Our hardcoded RSA key bypass and provider-guessing hack are unnecessary.

---

## Integration Contract (Source of Truth)

### JWT Claims

| Claim | Type | Source | Stability | Our Usage |
|-------|------|--------|-----------|-----------|
| `sub` | UUID | Broker `users.user_id` PK | Stable as a DB PK | `external_id` in JIT sync |
| `email` | string | From IdP | Can change (IdP-side) | JIT sync fallback match |
| `name` | string | From IdP, fallback = email | Can be email if IdP has no name | Display name in JIT sync |
| `tid` | string | From `tenants.tenant_key` | Always uppercase in DB | Tenant resolution, anti-spoofing |
| `jti` | UUID | Per-token unique ID | Unique per token | Log for audit traceability (v1) |
| `ext_provider` | string | `sso_configs.provider` | Free-form string | Store as `sso_provider` |
| `ext_issuer` | string | IdP's issuer URL | Stable per IdP | Not used by portal |
| `ext_subject` | string | IdP's user ID | Stable within an IdP | Not used by portal |
| `iss` | string | = Auth:Authority URL | Same URL as OIDC discovery base | JWT validation |
| `aud` | string | = `rgt-space-portal-api` | Fixed | JWT validation |

**Key identity facts:**
- `sub` = broker's user UUID, NOT the IdP's subject. Stable across IdP migrations (Google -> Azure AD).
- Same `sub` across all tenants (user record is shared, data access is tenant-gated).
- `name` may be the email address if IdP provides no display name.
- `ext_provider` values: `Google`, `AzureAD`, `MockIdP` (dev-only). Treat as free-form string, not enum.

### Tenant Isolation Contract

- `tid` claim is ALWAYS present (login requires `?tenant=` param, 400 if missing).
- Tenant IDs stored as UPPERCASE in broker DB.
- **Comparison rule:** Both sides `.ToUpperInvariant()` before comparing.
- JWT `tid` is authoritative (cryptographically signed). `X-Tenant` header is fallback for unauthenticated endpoints.
- Mismatch between JWT `tid` and `X-Tenant` header -> 403.

### Token Lifecycle

| Property | Value |
|----------|-------|
| Access token TTL | 15 minutes (configurable, trust `exp` claim) |
| Refresh token TTL | 14 days |
| Refresh rotation | One-time use, family-based reuse detection |
| Signing algorithm | RS256 (asymmetric RSA) |
| Token size | ~850-950 bytes typical, ~1KB worst case |
| Concurrent sessions | Fully supported (per-browser, per-tenant) |
| Logout | Revokes specific refresh token only, access token valid until `exp` |

### Broker Error Codes (Refresh Flow)

| Scenario | Error Code | HTTP | Frontend Action |
|----------|-----------|------|-----------------|
| Token not found / invalid | `REFRESH_TOKEN_NOT_FOUND` | 400 | Show error |
| Token expired | `REFRESH_TOKEN_EXPIRED` | 401 | Redirect to login |
| Reuse detected (family revoked) | `REFRESH_TOKEN_REUSE_DETECTED` | 409 | Redirect to login + security warning |
| Tenant mismatch | `REFRESH_TOKEN_TENANT_MISMATCH` | 403 | Redirect to login |
| User not found | `USER_NOT_FOUND` | 404 | Redirect to login |
| User deactivated | `USER_INACTIVE` | 403 | Redirect to login |
| Missing X-Tenant header | `TENANT_HEADER_MISSING` | 400 | Show error |
| Empty/malformed token | `VALIDATION_ERROR` | 400 | Show error |

### Multi-Tenant User Model

- Same email/user CAN authenticate under different tenants.
- Same `sub` UUID across tenants (it's the person, not tenant-scoped).
- Switching tenants requires full re-authentication (new OIDC flow).
- Refresh tokens are tenant-scoped (each has `tenant_id` in broker DB).
- Logout revokes only the specific refresh token, not all sessions.

### CORS Coordination

Frontend calls broker directly for:
- `GET /api/v1/auth/login?tenant=...&provider=...&callbackUrl=...`
- `GET /api/v1/auth/callback?code=...&state=...` (browser redirect from IdP)
- `POST /api/v1/auth/refresh`
- `POST /api/v1/auth/logout`
- `GET /api/v1/me`

JWKS/OIDC discovery endpoints already allow any origin.
Broker needs our frontend origins in CORS config. Our origins: `https://portal.rgtspace.com`, `https://admin.rgtspace.com`.

### Key Rotation

- Currently single-key JWKS (no grace period).
- When key rotates, old key disappears immediately.
- ASP.NET Core auto-refreshes JWKS on unknown `kid` (transparent retry).
- Brief burst of failed requests possible during rotation under load.
- Broker will add multi-key grace period (24h) before production key rotation.

### Broker Downtime Degradation

- Existing tokens validate (JWKS cached in ASP.NET Core middleware, 12h TTL).
- New logins and refreshes fail.
- **Risk:** If portal restarts during broker downtime, empty JWKS cache -> all auth fails.
- **v1 mitigation:** Coordinate maintenance windows.
- **v2 mitigation:** Persist JWKS to disk as fallback.

### Correlation ID

Broker accepts `X-Correlation-Id` header (valid GUID format). If present, uses it. If missing, generates new one. Propagate our correlation ID if we ever make server-to-server calls.

### Soft-Deleted User Re-Login (Edge Case)

**Broker layer (FIXED):** As of 2026-02-12, HandleCallback checks `is_active` before minting tokens. Inactive broker users get `403 Forbidden` with error code `USER_002`. The broker is now the first defense layer.

**Portal layer (still needed):** The broker's `users.is_active` and the portal's `users.is_deleted` are independent fields in separate databases. Portal soft-deleting a user does NOT set `is_active = false` on the broker. A user could be active on the broker but soft-deleted on the portal.

**Two-layer defense (v1):**
1. Broker blocks at login/refresh (is_active check) — DONE on their side (HandleCallback + refresh DB proc)
2. Portal blocks at JIT sync — reject if portal user is `is_deleted = TRUE` (Fix 7 below)

**Current bug:** JIT sync (`SyncOrGetUserAsync`) **reactivates** soft-deleted users instead of rejecting them. A soft-deleted portal user with a valid SSO token gets un-deleted on next request.

**Error response from broker for inactive user:**
```json
{ "status": 403, "errorCode": "USER_002", "title": "User Inactive", "detail": "User account is inactive." }
```

### Audience Claim

Always a single string `"rgt-space-portal-api"`. Never an array. Our `ValidAudience` (singular) config is correct.

### Clock Sync

No concerns. Cloud VMs use NTP internally (<100ms drift). Docker/K8s inherit host clock. 5-minute ClockSkew is generous. Both services use UTC for `nbf`/`exp`.

### Communication Direction

Strictly one-way: broker issues JWT -> portal validates. Broker NEVER calls the portal. No webhooks, no callbacks, no bidirectional communication planned for v1.

### API Versioning (Broker)

Claims are **append-only by convention** — new claims may be added, existing ones will not be removed or type-changed without explicit coordination. All 13 claims documented above are confirmed **Stable** by the broker team.

**Rules for portal:** If you see unknown claims in the JWT, ignore them. If expected claims are missing, fail gracefully with a clear error.

**TOKEN-CONTRACT.md** being created on the broker side to formalize this.

### Testing Strategy by Environment

| Environment | Auth Approach |
|-------------|--------------|
| CI (GitHub Actions) | `TestAuthHandler` — fake auth, no broker needed |
| Local dev | Real broker on localhost:7012, or TestAuthHandler |
| Staging | Real SSO flow against real broker |
| E2E manual | Broker's `GET /api/v1/test/mock-login` for quick tokens |

---

## Fixes Required (Portal Side)

### Fix 1: Case-Insensitive Tenant Comparison (CRITICAL)

**File:** `Rgt.Space.API/Middleware/TenantResolutionMiddleware.cs`

**Current (line 45):**
```csharp
if (!string.IsNullOrWhiteSpace(headerTenant) && headerTenant != jwtTid)
```

**Fix:**
```csharp
if (!string.IsNullOrWhiteSpace(headerTenant) &&
    !string.Equals(headerTenant, jwtTid, StringComparison.OrdinalIgnoreCase))
```

Also normalize the resolved tenant code before setting:
```csharp
tenantCode = jwtTid.ToUpperInvariant();
```

And normalize header fallback:
```csharp
tenantCode = context.Request.Headers[...].FirstOrDefault()?.ToUpperInvariant();
```

### Fix 2: Use `ext_provider` Claim (WARNING)

**File:** `Rgt.Space.API/Program.cs` (SSO OnTokenValidated)

**Current (line 184):**
```csharp
var provider = issuer?.Contains("localhost") == true ? "sso_broker" : "azuread";
```

**Fix:**
```csharp
var provider = context.Principal?.FindFirst("ext_provider")?.Value ?? "unknown";
```

This uses the actual `ext_provider` claim from the JWT. Values: `Google`, `AzureAD`, `MockIdP`, or any future IdP string. Treat as free-form, store as VARCHAR.

### Fix 3: Remove Hardcoded RSA Key (WARNING)

**File:** `Rgt.Space.API/Program.cs` (lines 131-158)

**Remove** the entire `if (builder.Environment.IsDevelopment())` block that hardcodes the RSA key. The OIDC discovery at `Authority = "https://localhost:7012"` with `RequireHttpsMetadata = false` (dev) handles key fetching automatically.

**Before:**
```csharp
// DEV ONLY: Hardcode RSA key to bypass Discovery issues
if (builder.Environment.IsDevelopment())
{
    try { ... rsa.ImportParameters ... } catch { ... }
}
```

**After:**
Delete the entire block. The `options.Authority` + `RequireHttpsMetadata = false` (dev) configuration is sufficient.

### Fix 4: Remove Broker DB Connection Strings (CLEANUP)

**File:** `Rgt.Space.API/appsettings.json`

**Remove these lines:**
```json
"RgtAuthPrototype": "Host=localhost;Database=rgt_auth_prototype;...",
"RgtAuthAudit": "Host=localhost;Database=rgt_auth_audit;..."
```

Confirmed by broker dev: portal should NOT have direct DB access to broker databases. The broker API is the boundary. Also remove from `appsettings.Development.json` if present.

### Fix 5: Log `jti` for Audit Traceability (LOW)

**File:** `Rgt.Space.API/Program.cs` (SSO OnTokenValidated, around line 175)

**Current:**
```csharp
logger.LogDebug("SSO Token Validated. Subject: {Subject}", subject);
```

**Fix:**
```csharp
var jti = context.Principal?.FindFirst("jti")?.Value;
logger.LogDebug("SSO Token Validated. Subject: {Subject}, JTI: {Jti}", subject, jti);
```

Optionally push `jti` to `HttpContext.Items` or LogContext for downstream audit correlation.

### Fix 6: Dead `"per-tenant"` Rate Limiter Policy (CLEANUP)

**File:** `Rgt.Space.API/Program.cs` (lines 322-330)

**Remove** the named `"per-tenant"` sliding window limiter policy. It's registered but never referenced by any endpoint. The global IP-based limiter (lines 332-348) is what actually runs.

### Fix 7: Reject Soft-Deleted Users in JIT Sync (CRITICAL)

**File:** `Rgt.Space.Infrastructure/Persistence/Services/Identity/IdentitySyncService.cs`

**Current behavior** (in `SyncOrGetUserAsync`, Step 2 — email match found):
```csharp
if (userEntity.IsDeleted)
{
    userEntity.Reactivate(); // ← BUG: undoes admin's soft-delete
}
userEntity.LinkSso(provider, externalId, email);
```

**Fix:**
```csharp
if (userEntity.IsDeleted)
{
    _logger.LogWarning(
        "Soft-deleted portal user {UserId} attempted SSO login. Rejecting. Provider: {Provider}, Email: {Email}",
        userEntity.Id, provider, email);
    return Guid.Empty; // Cascades: no x-local-user-id claim → no permissions → 403
}
userEntity.LinkSso(provider, externalId, email);
```

Also apply the same fix in `SyncUserFromSsoAsync` (the fire-and-forget overload):
```csharp
if (userEntity.IsDeleted)
{
    _logger.LogWarning("Soft-deleted portal user {UserId} attempted SSO sync. Rejecting.", userEntity.Id);
    return; // Do NOT reactivate
}
```

**Why Guid.Empty works:** `CurrentUser.Id` returns `Guid.Empty` when `x-local-user-id` is missing or invalid. `PermissionLoadingMiddleware` can't load permissions for `Guid.Empty` → user gets zero permissions → any endpoint with `Permissions()` returns 403.

**Defense layers after this fix:**
1. Broker: `HandleCallback` blocks inactive broker users (403 / USER_002)
2. Broker: `RefreshTokenService` blocks inactive users during refresh (403 / USER_INACTIVE) — being fixed this session
3. Portal: JIT sync rejects soft-deleted portal users (returns Guid.Empty → 403)

### Fix 8: Add `tid` to TestAuthHandler (LOW)

**File:** `Rgt.Space.Tests/Integration/Api/TestAuthHandler.cs`

**Current:** No `tid` claim — integration tests bypass tenant resolution entirely.

**Fix:** Add a `tid` claim so tests exercise the tenant resolution path:
```csharp
new("tid", "TEST_TENANT"),
```

This is non-blocking for v1 but was flagged during alignment check. Without it, tests don't exercise JWT-based tenant resolution, provider detection, or JIT sync.

### Fix 9: Remove Provider from External ID Lookup (MEDIUM)

**File:** `Rgt.Space.Infrastructure/Persistence/Dac/Identity/UserReadDac.cs` (GetByExternalIdAsync)

**Current SQL:**
```sql
WHERE sso_provider = @Provider AND external_id = @ExternalId AND is_deleted = FALSE
```

**Problem:** The broker's `sub` is IdP-agnostic (it's the broker's `users.user_id`, not the IdP's subject). Same UUID regardless of Google, Azure AD, or future providers. But our lookup keys on `(sso_provider, external_id)`, so when a user switches IdPs (Google → Azure AD), Step 1 misses and Step 2 re-links with the new provider — causing a flip-flop on every IdP switch.

**Fix (DAC):**
```sql
WHERE external_id = @ExternalId AND is_deleted = FALSE
```

**Fix (interface):** Change `GetByExternalIdAsync(string provider, string externalId, ct)` → `GetByExternalIdAsync(string externalId, ct)`. Remove provider from the lookup signature.

**Fix (index):** Migration to replace the partial unique index:
```sql
-- Drop old compound index
DROP INDEX IF EXISTS idx_users_sso_active;
-- Create new index on external_id alone
CREATE UNIQUE INDEX idx_users_sso_active
    ON users(external_id)
    WHERE is_deleted = FALSE AND external_id IS NOT NULL;
```

**Fix (IdentitySyncService):** Update both `SyncOrGetUserAsync` and `SyncUserFromSsoAsync`:
```csharp
// Before:
var existingUserReadModel = await _userRead.GetByExternalIdAsync(provider, externalId, ct);
// After:
var existingUserReadModel = await _userRead.GetByExternalIdAsync(externalId, ct);
```

Keep `sso_provider` as informational — still set via `LinkSso()` and `UpdateFromSso()` (tracks last-used provider), but never used as a lookup key.

**Broker team confirmed:** `sub` is stable across IdP migrations. `ext_provider` is metadata about which IdP was used for THIS login, not a stable identity key.

---

## Architecture Decisions (ALL RESOLVED — 2026-02-12)

| # | Decision | Answer | Impact on Portal |
|---|----------|--------|-----------------|
| D1 | Production Broker URL | TBD — IP-based (no domain purchased) | Keep `localhost:7012` for dev. Config via `Auth:Authority` env var in prod. |
| D2 | Shared root domain? | **No** — separate URLs, separate services | No cookie sharing. CORS must be explicit on both sides. |
| D3 | Per-tenant RBAC? | **Global roles for v1** — portal's decision entirely | No schema change needed. Broker won't include role claims. Flag per-tenant for v2. |
| D4 | Server-to-server API? | **Not v1** — one-way integration only | No admin endpoints. Manual DB update for broker-side deactivation if needed. |
| D5 | Soft-deleted user re-login? | **Reject at portal middleware** (defense in depth) | Broker blocks at login (USER_002/403). Portal blocks at request time if `is_deleted = TRUE`. |
| D6 | JWKS disk persistence? | **Not v1** — coordinate maintenance windows | Single-key JWKS, no rotation grace period. Handle 401 → re-login gracefully. |
| D7 | HandleCallback is_active? | **FIXED (2026-02-12)** | Inactive broker users now get 403 at login. |
| D8 | Token contract? | **In progress** — TOKEN-CONTRACT.md being created | Use Round 4 F29 stable claims list until formalized. |

### CORS Status
- **Dev:** Broker allows `localhost:3000` and `localhost:5173`. If portal frontend uses a different port, coordinate with broker team.
- **Prod:** Origins TBD until deployment URLs are known. Both sides must update CORS config before go-live.

### Refresh is_active Bug (Broker Side)
- Still pending fix in `RefreshTokenService.cs:159`. Broker dev aware.
- `rotate_refresh_token` stored proc already checks, but the C# service layer doesn't.

---

## Broker-Side Fixes (Tracked on Their Side)

These are NOT our responsibility but affect us:

| Issue | Status | Impact on Us |
|-------|--------|-------------|
| ~~`is_active` not checked in HandleCallback~~ | **FIXED (2026-02-12)** — returns USER_002 / 403 | Inactive users now blocked at login |
| `is_active` not checked during refresh | **In progress (this session)** — fixing DB proc + C# service layer | Deactivated users can currently refresh indefinitely. New error: `USER_INACTIVE` / 403 |
| Single-key JWKS (no rotation grace period) | Pending — add multi-key support with 24h overlap | Brief auth failures during key rotation |
| No rate limiting on broker endpoints | Pending — add ASP.NET Core rate limiter | Our frontend refresh calls could be throttled |
| CORS hardcoded to localhost | Pending — move to appsettings.json, add prod origins | Frontend can't call broker in prod without this |
| `expiresIn` hardcoded to 900 in refresh response | Pending — fix to read from config | Trust `exp` claim, not response field |
| No API versioning / claim contract | In progress — TOKEN-CONTRACT.md being created | Claims confirmed append-only, all 13 stable |

---

## What's Already Correct (No Changes Needed)

| Area | Status | Detail |
|------|--------|--------|
| `MapInboundClaims = false` | Correct | Prevents `tid` -> long Microsoft URI mapping |
| Issuer = Authority URL | Correct | Broker confirms they're always the same |
| ClockSkew 5 minutes | Correct | Matches broker recommendation |
| RequireHttpsMetadata gated | Correct | `!IsDevelopment()` |
| JIT sync logic | Correct (after Fix 7) | `sub` first -> `email` fallback -> reject if deleted -> link/create |
| `x-local-user-id` claim | Correct | Added after JIT sync for downstream use |
| Dual auth (SSO + Local) | Correct | Keep for v1, deprecate local when SSO is stable |
| No refresh logic | Correct | Frontend handles refresh with broker directly |
| RBAC as separate layer | Correct | `PermissionLoadingMiddleware` after auth |
| `CurrentUser.TenantKey` reads `tid` | Correct | Short claim name preserved by MapInboundClaims=false |
| `ValidAudience` (singular) | Correct | Broker always issues single-string `aud`, never array |
| ClockSkew 5 minutes | Correct | Cloud NTP drift <100ms, both sides use UTC |
| TestAuthHandler for CI | Correct | Broker has no test mode — TestAuthHandler is the right pattern |

---

## Implementation Estimate

| Fix | Effort | Risk |
|-----|--------|------|
| Fix 1: Case-insensitive tenant | 5 min | Low (string comparison change) |
| Fix 2: ext_provider claim | 5 min | Low (claim extraction) |
| Fix 3: Remove hardcoded RSA key | 5 min | Medium (verify OIDC discovery works on localhost) |
| Fix 4: Remove broker DB conn strings | 2 min | Zero |
| Fix 5: Log jti | 3 min | Zero |
| Fix 6: Dead rate limiter policy | 2 min | Zero |
| Fix 7: Reject soft-deleted in JIT sync | 10 min | Medium (behavior change — reactivation removed) |
| Fix 8: Add tid to TestAuthHandler | 3 min | Low |
| Fix 9: Remove provider from external ID lookup | 15 min | Medium (DAC + interface + index migration + service) |
| **Total** | **~50 min** | **Test: build + 146 tests + migration** |

**Prerequisite:** Fix 3 requires the SSO broker to be running on `https://localhost:7012` with working OIDC discovery. Verify before removing the hardcoded key.

---

## Alignment Check Results

### Round 5 (A1-A10) — Broker Team Reviewed Our Implementation

**Confirmed Aligned:**
- Refresh flow: Portal API doesn't call refresh — correct, frontend-only
- Tenant casing: Both sides normalizing to `.ToUpperInvariant()` — broker doing their side this session
- JWKS/OIDC: Remove hardcoded key — broker confirms discovery works on localhost
- TestAuthHandler pattern: Correct approach for CI
- No S2S, no correlation propagation, no JWKS persistence — all skipped for v1

**Gaps Found → New Fixes:**
1. Fix 2 upgraded to CRITICAL — `issuer.Contains("localhost")` classifies everyone as `"azuread"` in prod
2. Fix 7 added — JIT sync reactivation bug (soft-deleted users get un-deleted)
3. Fix 8 added — TestAuthHandler missing `tid` claim

### Round 6 (B1-B10) — Edge Cases & Integration Seams

**Confirmed Aligned:**
- B1: Multi-scheme auth with `x-local-user-id` convergence — sound design
- B4: Email uniqueness model — both sides global, one user per email, consistent
- B5: 15-min post-logout window — known, accepted for v1
- B8: JWKS lazy loading — app starts fine without broker
- B9: Global permissions — consistent with D3 decision

**Gaps Found → New Fix:**
- B7/Fix 9: Provider flip-flop on IdP switch — match on `external_id` alone, not `(sso_provider, external_id)`

**Tracked for Future (Not v1):**
- B2: JIT sync overwrites admin-edited display_name/email (LOW — IdP is source of truth for SSO users)
- B3: Concurrent JIT sync race can throw on duplicate insert (LOW — self-healing, unique index saves us)
- B6: JIT sync failure returns 403 instead of 401 (LOW — `context.Fail()` would be more accurate)
- B10: No alerts for auth failures (LOW — operational monitoring not yet configured)

### Alignment Surface — EXHAUSTED (2026-02-12)

Both teams confirm all integration seams covered across 6 rounds:
- JWT structure and claims (Rounds 1-4)
- Error codes and HTTP status mapping (F21)
- Tenant isolation model (F3, F4, A4, B4)
- User lifecycle (creation, deactivation, deletion) (F9, F24, A3, A5)
- Session management (login, refresh, logout, concurrent) (F8, F10, F19, F20, B5)
- Account linking and IdP migration (F6, F16, F17, B7)
- Testing infrastructure (A10)
- Operational concerns (F11, F12, F13, F26, F27, B8, B10)

### Broker Actions (This Session)
- Fixing tenant casing normalization (broker middleware)
- Fixing refresh `is_active` check (DB proc + C# service layer) — new error: `USER_INACTIVE` / 403
- Creating TOKEN-CONTRACT.md (stable claims documentation)

---

## Test Plan

1. `dotnet build` — 0 errors
2. `dotnet test` — 146/146 green (existing tests use TestAuthHandler, not real SSO)
3. Manual: Start SSO broker + portal, login via Google, verify JWT validation works without hardcoded key
4. Manual: Send `X-Tenant: 7eleven` with JWT `tid: 7ELEVEN` — should succeed (case-insensitive)
5. Manual: Send `X-Tenant: BURGERKING` with JWT `tid: 7ELEVEN` — should 403
6. Verify `ext_provider` appears in user record after JIT sync
7. Manual: Soft-delete a user → attempt SSO login → verify 403 (not reactivation)
8. Verify `jti` appears in structured logs after SSO login
