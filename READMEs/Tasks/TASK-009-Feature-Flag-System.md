# TASK-009: Feature Flag System (The Gatekeeper)

**Status:** ✅ Ready to Build  
**Type:** Feature / Tech Debt  
**Owner:** @Khumeren  
**Last Updated:** 2026-01-26

---

## 🧠 Context & Mental Model

We are implementing a **runtime control plane** to decouple code deployment from feature release. Think of this as a **deterministic Waterfall Gatekeeper**:

```
Result = Global ∧ Client ∧ UserOverride
```

- **Global** is the master kill switch (engineering control)
- **Client** is subscription/entitlement (commercial control)  
- **UserOverride** refines access within the client gate (beta/support control)

This enables:
- **Dark launches**: Pilot clients / pilot users before general release
- **Instant kill-switch**: Disable broken modules without redeploy
- **Subscription gating**: Premium features gated per client organization

> "If code is the engine, Feature Flags are the dashboard toggles. We can dark-launch features to 1% of users, kill a buggy module instantly, or gate premium features per client—all by flipping a bit in the database."

---

## 📖 Glossary (Critical Distinction)

| Term | Meaning | Example |
|------|---------|---------|
| **Tenant** (`tid` / `X-Tenant`) | SSO application context / legacy sales DB routing | `RGT_SPACE_PORTAL` |
| **Client** | Business organization row in `clients` table | `ACME`, `TOYOTA`, `BURGERKING` |

🚨 **Feature flags apply to Clients, NOT Tenants.**

The JWT `tid` claim identifies the SSO application context (always `RGT_SPACE_PORTAL` for this app), not the business client. Client context must be derived from route parameters or database lookups.

---

## ⛔ Non-Goals (Scope Fence)

- ❌ **No Multitenant Framework Adoption**: No Finbuckle, no DB-per-tenant. This remains the Logical Monolith.
- ❌ **No SSO Broker Changes**: RS256/JWKS/OIDC contract stays unchanged.
- ❌ **No Real-time Pub/Sub**: Phase 1 uses TTL-based caching (60s), not WebSocket updates.
- ❌ **FeatureGate is NOT access control**: It does not validate "user belongs to client." The caller must authorize.

---

## 📐 Architecture Alignment (v1.4 Laws + Portal Routing Convention)

### Data Physics

| Aspect | Convention |
|--------|------------|
| **Primary Keys** | UUID v7 via `uuid_generate_v7()` |
| **Timestamps** | `TIMESTAMP WITHOUT TIME ZONE` with `DEFAULT (now() AT TIME ZONE 'utc')` — Portal Routing style |
| **Soft Delete** | `is_deleted = FALSE` governs uniqueness ("Zombie Constraints") |
| **Naming** | `snake_case` (DB), `PascalCase` (C#), `camelCase` (JSON) |
| **Feature Codes** | `UPPER_SNAKE_CASE`, enforced by CHECK constraint |

> **Note**: Feature flags follow Portal Routing module audit style (UTC-normalized). UAM/RBAC migration to UTC is deferred.

---

## 🗄️ Database Schema (PortalDb / Logical Monolith)

### A) `features` — Global Registry + Kill Switch

**Purpose**: Define features and global control.

```sql
CREATE TABLE features (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    code VARCHAR(50) NOT NULL,  -- e.g., "DASHBOARD_V2" (UPPER_SNAKE_CASE)
    name VARCHAR(255) NOT NULL,
    description TEXT NULL,
    
    -- Configuration
    is_active BOOLEAN NOT NULL DEFAULT FALSE,       -- Master kill switch (default OFF for safe rollout)
    requires_client BOOLEAN NOT NULL DEFAULT TRUE,  -- FALSE only for SYS_* system features
    
    -- Audit Trail (Portal Routing Style)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),
    
    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id),
    
    -- Enforce UPPER_SNAKE_CASE
    CONSTRAINT features_code_format_chk CHECK (code = upper(code) AND code ~ '^[A-Z][A-Z0-9_]*$')
    
    -- Optional: Enforce SYS_* naming for system features (uncomment to enable)
    -- , CONSTRAINT features_requires_client_naming_chk CHECK (
    --     requires_client = TRUE OR code LIKE 'SYS_%'
    -- )
);

-- Zombie Constraint: code unique among non-deleted
CREATE UNIQUE INDEX idx_features_code_active ON features(code) WHERE is_deleted = FALSE;

-- Performance Index
CREATE INDEX idx_features_is_active ON features(is_active) WHERE is_deleted = FALSE;
```

**Business Rules**:
- `is_active = FALSE` → Feature disabled globally (overrides everything)
- `requires_client = FALSE` → System feature, no client gate (use `SYS_*` prefix convention)
- Default `is_active = FALSE` ensures safe rollout (opt-in activation)
- **Soft-deleted features (`is_deleted = TRUE`) are treated as not found** → `FEATURE_NOT_FOUND`
- **Features are never hard-deleted in Phase 1** (soft-delete only). FK constraints prevent accidental hard-deletes while dependencies exist.

### B) `client_features` — Client Subscription / Entitlement

**Purpose**: Whether a client is subscribed/enabled for a feature.

```sql
CREATE TABLE client_features (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- References
    client_id UUID NOT NULL REFERENCES clients(id) ON DELETE RESTRICT,
    feature_id UUID NOT NULL REFERENCES features(id) ON DELETE RESTRICT,
    
    -- Configuration
    is_enabled BOOLEAN NOT NULL DEFAULT TRUE,
    
    -- Audit Trail (Portal Routing Style)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id),
    updated_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    updated_by UUID NULL REFERENCES users(id),
    
    -- Soft Delete
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMP WITHOUT TIME ZONE NULL,
    deleted_by UUID NULL REFERENCES users(id)
);

-- Zombie Constraint: one active subscription per client+feature
CREATE UNIQUE INDEX idx_client_features_active 
    ON client_features(client_id, feature_id) 
    WHERE is_deleted = FALSE;

-- Performance Indexes
CREATE INDEX idx_client_features_client ON client_features(client_id) WHERE is_deleted = FALSE;
CREATE INDEX idx_client_features_feature ON client_features(feature_id) WHERE is_deleted = FALSE;
```

**Evaluation Stance: Opt-In Per Client**
- Row absence → `OFF` (`CLIENT_NOT_SUBSCRIBED`)
- Soft-deleted row → counts as absent (`OFF`)
- Row exists with `is_enabled = TRUE` → `ON`
- **Soft-deleted client (`clients.is_deleted = TRUE`)** → treated as row absence (`CLIENT_NOT_SUBSCRIBED`) at runtime. No new reason code needed.

> **Runtime vs Admin behavior**: Runtime evaluation treats deleted clients as unsubscribed for stability (graceful degradation). Admin operations reject mutations with `CLIENT_DELETED` error to prevent enabling features for dead clients.

### C) `user_feature_overrides` — Targeted User Refinement

**Purpose**: Beta/support refinements inside the client gate.

```sql
CREATE TABLE user_feature_overrides (
    -- Identity
    id UUID PRIMARY KEY DEFAULT uuid_generate_v7(),
    
    -- References
    user_id UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    feature_id UUID NOT NULL REFERENCES features(id) ON DELETE RESTRICT,
    
    -- Configuration
    override_state VARCHAR(20) NOT NULL
        CONSTRAINT user_feature_overrides_state_chk 
        CHECK (override_state IN ('FORCE_ON', 'FORCE_OFF')),
    reason TEXT NULL,
    
    -- Thin Audit (matches user_permission_overrides pattern)
    created_at TIMESTAMP WITHOUT TIME ZONE NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    created_by UUID NULL REFERENCES users(id)
);

-- One override per user+feature (hard delete when removed)
CREATE UNIQUE INDEX idx_user_feature_overrides_active 
    ON user_feature_overrides(user_id, feature_id);

-- Performance Index
CREATE INDEX idx_user_feature_overrides_user ON user_feature_overrides(user_id);
```

**Override Semantics**:
- Row absence → `INHERIT` (user gets client/global decision)
- `FORCE_ON` → User gets access (within client+global gate)
- `FORCE_OFF` → User denied (even if client has access)
- **No explicit `INHERIT` state stored** — row absence is cleaner

---

## 🧠 Evaluation Logic (Authoritative)

### The Waterfall Formula

```
Result = Global ∧ Client ∧ UserOverride
```

### Complete Truth Table

| Global `is_active` | `requires_client` | Client Row? | Client `is_enabled` | User Override | **Result** | **Reason Code** |
|:-:|:-:|:-:|:-:|:-:|:-:|:--|
| ❌ OFF | - | - | - | - | **OFF** | `GLOBAL_OFF` |
| ✅ ON | ❌ FALSE | - | - | None | **ON** | `GRANTED` |
| ✅ ON | ❌ FALSE | - | - | `FORCE_OFF` | **OFF** | `USER_FORCE_OFF` |
| ✅ ON | ❌ FALSE | - | - | `FORCE_ON` | **ON** | `USER_FORCE_ON` |
| ✅ ON | ✅ TRUE | ❌ NO | - | - | **OFF** | `CLIENT_NOT_SUBSCRIBED` |
| ✅ ON | ✅ TRUE | ✅ YES | ❌ OFF | - | **OFF** | `CLIENT_OFF` |
| ✅ ON | ✅ TRUE | ✅ YES | ✅ ON | None | **ON** | `GRANTED` |
| ✅ ON | ✅ TRUE | ✅ YES | ✅ ON | `FORCE_OFF` | **OFF** | `USER_FORCE_OFF` |
| ✅ ON | ✅ TRUE | ✅ YES | ❌ OFF | `FORCE_ON` | **OFF** | `CLIENT_OFF` |
| ✅ ON | ✅ TRUE | ✅ YES | ✅ ON | `FORCE_ON` | **ON** | `USER_FORCE_ON` |
| Feature not found | - | - | - | - | **OFF** | `FEATURE_NOT_FOUND` |

### 🚨 Critical Rules

1. **FORCE_ON cannot bypass Global or Client gates**  
   User overrides only refine within permitted scope. Commercial/global gates are inviolable.

2. **System features (`requires_client = FALSE`) still honor user overrides**  
   For `requires_client = FALSE`: client gate is skipped, but user overrides apply **only if `userId` is provided** to `IsEnabledAsync(...)`. `IsEnabledGloballyAsync()` is override-blind by definition.  
   ⚠️ **If a system feature must respect per-user overrides**, callers must use `IsEnabledAsync(featureCode, anyClientId, userId)` — NOT `IsEnabledGloballyAsync()`.

3. **Global method fail-fast for client-scoped features**  
   If `IsEnabledGloballyAsync()` called for `requires_client = TRUE` → return `OFF` with `REQUIRES_CLIENT`.

4. **For `requires_client = FALSE`, `clientId` is ignored**  
   The `clientId` parameter is still required by `IsEnabledAsync()` method signature for consistency, but it is not used in evaluation. This prevents devs from thinking `clientId` influences system features.

5. **Soft-deleted entities treated as absent**  
   - `features.is_deleted = TRUE` → `FEATURE_NOT_FOUND`
   - `clients.is_deleted = TRUE` → `CLIENT_NOT_SUBSCRIBED` (runtime)
   - `client_features.is_deleted = TRUE` → row absent

6. **`Guid.Empty` clientId guard**  
   If `requires_client = TRUE` and caller passes `Guid.Empty` as `clientId` → return `OFF` with `CLIENT_NOT_SUBSCRIBED` and log warning. Do not query database with empty GUID.

---

## 🧭 Client Context Source Rules (Caller Responsibility)

FeatureGate **trusts** the `clientId` it receives. It does **NOT** validate authorization.

| Handler Context | ClientId Source |
|-----------------|-----------------|
| `/clients/{clientId}/...` | Route parameter |
| `/projects/{projectId}/...` | **DB lookup**: `projects.client_id` (required) |
| CREATE operations targeting client | Body `ClientId` allowed |
| System/platform endpoints | Use `IsEnabledGloballyAsync()` only |

🚨 **Never**: Accept `clientId` from request body for read/update authorization-sensitive operations.

---

## 🔍 Observability & Tracking

### Combo-Break Logging

Every decision **must** be logged to the Combo-Break tracker:

```
flag:{CODE}=ON|OFF ({REASON})
```

Example: `flag:DASHBOARD_V2=OFF (CLIENT_NOT_SUBSCRIBED)`

### Reason Code Canon (Do Not Invent New Ones)

| Reason Code | Meaning |
|-------------|---------|
| `FEATURE_NOT_FOUND` | Feature code doesn't exist in database |
| `GLOBAL_OFF` | `features.is_active = FALSE` |
| `REQUIRES_CLIENT` | Global method called for client-scoped feature |
| `CLIENT_NOT_SUBSCRIBED` | No `client_features` row exists |
| `CLIENT_OFF` | `client_features.is_enabled = FALSE` |
| `USER_FORCE_OFF` | User override is `FORCE_OFF` |
| `USER_FORCE_ON` | User override is `FORCE_ON` |
| `GRANTED` | All gates passed |

> **Note**: `ReasonCode` applies only to `FeatureDecision` objects. Admin API endpoints may return separate `ErrorCode` values (e.g., `CLIENT_DELETED`, `FEATURE_DELETED`) which are NOT part of this canon table.

### TrackedDacExecutor

All FeatureDac calls must be tracked:
```
repo:FeatureDac:{Method}
```

---

## 🧩 Interface Contracts (Authoritative)

### IFeatureGate

```csharp
public interface IFeatureGate
{
    /// <summary>
    /// Evaluate feature for a specific client + optional user override.
    /// Use for client-specific operations (most common case).
    /// </summary>
    Task<FeatureDecision> IsEnabledAsync(
        string featureCode,
        Guid clientId,
        Guid? userId = null,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluate global-only feature (requires_client = FALSE).
    /// Use for system features like SYS_COMBO_BREAK_DEBUG.
    /// Returns OFF with REQUIRES_CLIENT if feature.requires_client = TRUE.
    /// </summary>
    Task<FeatureDecision> IsEnabledGloballyAsync(
        string featureCode,
        CancellationToken ct = default);
}

public sealed record FeatureDecision(
    bool IsEnabled,
    string ReasonCode,
    string FeatureCode);
```

### IFeatureDac

```csharp
public interface IFeatureDac
{
    /// <summary>
    /// Get feature by code. Query MUST filter is_deleted = FALSE.
    /// </summary>
    Task<FeatureReadModel?> GetByCodeAsync(string code, CancellationToken ct);
    
    /// <summary>
    /// Get client subscription. Query MUST filter is_deleted = FALSE.
    /// </summary>
    Task<ClientFeatureReadModel?> GetClientFeatureAsync(Guid clientId, Guid featureId, CancellationToken ct);
    
    /// <summary>
    /// Get user override. No is_deleted filter needed (table has no soft-delete column).
    /// </summary>
    Task<UserOverrideReadModel?> GetUserOverrideAsync(Guid userId, Guid featureId, CancellationToken ct);
    
    /// <summary>
    /// Returns features where is_active = TRUE AND is_deleted = FALSE.
    /// For admin UI "all features" list, use GetAllNonDeletedAsync instead.
    /// </summary>
    Task<IReadOnlyList<FeatureReadModel>> GetAllGloballyEnabledAsync(CancellationToken ct);
    
    /// <summary>
    /// Returns all non-deleted features (for admin UI).
    /// </summary>
    Task<IReadOnlyList<FeatureReadModel>> GetAllNonDeletedAsync(CancellationToken ct);
}
```

### Governance Rule

> **FeatureGate is called ONLY from MediatR handlers, FastEndpoints, or services.  
> DACs remain pure data access with no business logic.**

---

## 🗑️ Caching Strategy

### Cache Key Scheme

| Cache Entry | Key Format | TTL |
|-------------|-----------|-----|
| Feature by code | `Feature:{code}` | 60s |
| Client subscription | `ClientFeature:{clientId}:{featureId}` | 60s |
| User override | `UserOverride:{userId}:{featureId}` | 60s |

### Requirements

1. **Negative caching**: Cache "not found" results (null markers) to avoid DB hammering
2. **Cache inputs, not decisions**: Cache evaluation inputs (feature row, client row, override row) with negative caching; compute `FeatureDecision` deterministically from cached inputs. This ensures userId-specific overrides are always evaluated correctly.
3. **Stampede protection**: Use `IMemoryCache.GetOrCreateAsync` pattern
4. **Early exit on feature not found**: If feature not found, do NOT query `client_features` or `user_feature_overrides`. Cache the not-found result for 60s and return immediately.

---

## 🛡️ Governance & Audit

### Audit Invariant (Non-Negotiable)

> **All feature mutations MUST be executed through MediatR commands.**

This ensures `audit_log` captures:
- Who toggled
- What feature
- What client (if applicable)
- Request/response/delta

Feature DACs are **read-only** for normal application flow.

### requires_client Policy

| `requires_client` | Allowed For | Naming Convention |
|-------------------|-------------|-------------------|
| `FALSE` | System/platform features only | `SYS_*` prefix (e.g., `SYS_COMBO_BREAK_DEBUG`) |
| `TRUE` (default) | All product/module features | Any other code |

### Timestamp Management

> **`updated_at` / `updated_by` must be set by application code on every mutation.**  
> No database trigger in Phase 1. Default values only apply on INSERT.

### Feature Code Normalization

> **All public APIs and FeatureGate must normalize feature codes:**  
> `code.Trim().ToUpperInvariant()` before any cache key generation or database lookup.  
> This prevents cache key duplication (e.g., `DASHBOARD_V2` vs `dashboard_v2`).

---

## 📝 Implementation Plan

### Phase 1 — Schema + Registration

- [ ] Create migration: `features`, `client_features`, `user_feature_overrides` tables
- [ ] Register `IFeatureGate`, `IFeatureDac` in `Extensions.cs`
- [ ] Add seed entry for test flag: `DASHBOARD_V2` with `is_active = FALSE`

### Phase 2 — FeatureDac + FeatureGate + Cache

- [ ] Implement `FeatureDac` using **System DB pattern** + `TrackedDacExecutor`
- [ ] Implement `FeatureGate` waterfall logic with Combo-Break logging
- [ ] Implement caching: `IMemoryCache` with 60s TTL + negative caching

### Phase 3 — Admin Endpoints (FastEndpoints)

- [ ] Endpoints for:
  - Global toggle (`PUT /api/v1/features/{code}/toggle`)
  - Client subscription toggle (`PUT /api/v1/clients/{clientId}/features/{featureCode}/toggle`)
  - Set user override (`POST /api/v1/users/{userId}/feature-overrides`)
  - Clear user override (`DELETE /api/v1/users/{userId}/feature-overrides/{featureCode}`)
- [ ] **Toggle Mutation Rules**:
  - **Global toggle**: Flip `features.is_active`; never soft-delete for toggle
  - **Client toggle (upsert)**: 
    - If no `client_features` row exists → INSERT with `is_enabled = TRUE`
    - If row exists → UPDATE `is_enabled = NOT is_enabled`
    - Soft-delete is NOT used for toggling (only for deprecation/cleanup)
  - **User override**: INSERT/UPDATE override row; DELETE removes row (hard delete)
- [ ] **Guardrails**:
  - Validate `clients.is_deleted = FALSE` before allowing client feature toggles
  - Return `CLIENT_DELETED` error if client is soft-deleted
  - Validate `features.is_deleted = FALSE` before any toggle
  - Validate `users.is_deleted = FALSE` before setting user overrides
- [ ] Permissions (RBAC DOT convention):
  - `FEATURES.LIST.VIEW`
  - `FEATURES.GLOBAL.EDIT`
  - `FEATURES.CLIENT.EDIT`
  - `FEATURES.OVERRIDE.INSERT`
  - `FEATURES.OVERRIDE.DELETE`

### Phase 4 — Verification

- [ ] **Waterfall test**: Verify FORCE_ON cannot bypass Client OFF
- [ ] **Combo-Break test**: Verify decisions appear as `flag:{CODE}=...`
- [ ] **Cache test**: Verify cache-hit path < 1ms overhead
- [ ] **Audit test**: Verify all toggle operations produce `audit_log` entries

---

## 🎯 Success Criteria

| Criterion | Metric |
|-----------|--------|
| ✅ No redeploy required | Toggle global/client/user access via admin API |
| ✅ Explainable decisions | Combo-Break shows `flag:{CODE}=ON\|OFF (reason)` |
| ✅ No SSO impact | Zero changes to SSO Broker contract |
| ✅ Performance | Cache-hit evaluation < 1ms overhead |
| ✅ Audit trail | All mutations produce `audit_log` entries |

---

## ⚠️ Known Security Debt (Tracked, Out of Scope)

### SalesReadDac Tenant Trust

**Risk**: Authenticated user can spoof `X-Tenant` header to access different tenant's Sales database.

**Root Cause**: `TenantResolutionMiddleware` runs before `UseAuthentication()`, so JWT `tid` claim is never read for authenticated requests. Tenant is derived from header/query.

**Minimal Fix** (outside TASK-009):
- Require validated JWT-derived tenant for Sales DB routing
- Disallow header-based tenant override for authenticated requests

**Status**: Tracked in security backlog. Feature flags are **not dependent** on tenant middleware correctness (clientId is explicit).

---

## 📚 References

- [Architecture v1.4](../Architecture.md) — Laws and conventions
- [Portal Routing Schema](../SQL/PostgreSQL/Tables/clients.sql) — Audit column template
- [SSO Architecture](../BusinessRules/SSO-Architecture.md) — JWT claims and `tid` meaning
