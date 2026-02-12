# Business Rules: Feature Flags (Gatekeeper)

Status: DRAFT
Last Updated: 2026-01-28
Owner: RGT Space Portal

## 1) Purpose
Provide a runtime control plane that decouples feature release from code deployment.
Feature flags determine availability; RBAC still determines authorization.

## 2) Definitions
- Tenant: SSO application context (e.g., RGT_SPACE_PORTAL). Not used for business data isolation.
- Client: Business organization (row in clients table). This is the isolation boundary for feature flags.
- Project: Owned by a Client (projects.client_id).

## 3) Scope
- Feature flags apply to Clients and Users.
- Feature flags are NOT an access control system.
- Feature flags must not validate user membership or authorization.

## 4) Non-Goals
- No multi-tenant DB strategy.
- No SSO broker changes.
- No real-time pub/sub in Phase 1 (TTL cache only).

## 5) Data Model (Logical)
- features: Global registry and master kill switch.
- client_features: Client subscription/entitlement for a feature.
- user_feature_overrides: User-level refinement within the client gate.

Data physics:
- Primary keys: UUID v7 via uuid_generate_v7().
- Timestamps: TIMESTAMP WITHOUT TIME ZONE with DEFAULT (now() AT TIME ZONE 'utc').
- Soft delete: is_deleted with partial unique indexes.
- Feature codes: UPPER_SNAKE_CASE.

## 6) Evaluation Logic
Waterfall formula:
Result = Global AND Client AND UserOverride

- Global: features.is_active must be TRUE.
- Client: client_features row must exist and is_enabled must be TRUE.
- UserOverride: FORCE_ON / FORCE_OFF refines the result.

Critical rule: FORCE_ON cannot bypass Global or Client gates.

## 7) System Features
- features.requires_client = FALSE indicates a system feature.
- System features skip the Client gate, but still honor user overrides when a userId is provided.
- If a client-scoped feature is evaluated using a global-only method, return OFF with REQUIRES_CLIENT.

## 8) Caller Responsibilities (Client Context)
FeatureGate trusts the clientId it receives. It does not validate authorization.
- /clients/{clientId}/... uses route clientId.
- /projects/{projectId}/... must resolve project -> clientId by DB lookup.
- Read/update operations must not accept clientId from request body for authorization-sensitive access.

## 9) Reason Codes (Canonical)
- FEATURE_NOT_FOUND
- GLOBAL_OFF
- REQUIRES_CLIENT
- CLIENT_NOT_SUBSCRIBED
- CLIENT_OFF
- USER_FORCE_OFF
- USER_FORCE_ON
- GRANTED

## 10) Logging and Audit
- Every evaluation emits: flag:{CODE}=ON|OFF (REASON)
- All mutations must go through MediatR commands to ensure audit_log entries.

## 11) Caching
- Cache inputs, not decisions (feature row, client row, override row).
- Negative caching is required for not-found rows.
- TTL: 60 seconds (configurable by environment if needed).

Cache keys:
- Feature:{code}
- ClientFeature:{clientId}:{featureId}
- UserOverride:{userId}:{featureId}

## 12) Admin Operations (Behavioral Rules)
- Global toggle: flip features.is_active (no soft delete).
- Client toggle: upsert client_features; do not soft delete to toggle.
- User override: insert/update override row; delete removes override.

Guardrails:
- clients.is_deleted = TRUE -> reject client toggle (CLIENT_DELETED).
- features.is_deleted = TRUE -> reject toggle (FEATURE_DELETED).
- users.is_deleted = TRUE -> reject user override.

## 13) Permissions (RBAC)
- FEATURES.LIST.VIEW
- FEATURES.GLOBAL.EDIT
- FEATURES.CLIENT.EDIT
- FEATURES.OVERRIDE.INSERT
- FEATURES.OVERRIDE.DELETE

## 14) No-Redeploy Guarantee
After DB changes, behavior updates within cache TTL without appsettings edits or redeploys.

END



- We already have global RBAC; it answers “can you do this?”
    and stays stable.
- Feature flags add “is this feature available for this
    client/user?” without redeploys.
  - Flags are controlled in DB with a deterministic waterfall:
    Global ∧ Client ∧ UserOverride.
  - Client context is explicit (route/DB lookup), not the SSO
    tenant header.
  - Changes propagate in ~60s via cache TTL; no appsettings or
    code changes needed.

  What’s over‑engineered (if speed is the priority)

  - Reason‑code canon + Combo‑Break logging (great for audit/
    support, heavy for MVP).
  - Negative caching + stampede protection (solid for scale,
    optional to start).
  - User overrides (powerful, but can be Phase 2).

  What’s feasible now

  - Schema + DAC + FeatureGate are clean and isolated.
  - Admin endpoints are standard CRUD with RBAC guards.
  - No SSO changes needed; no impact on auth contracts.

  Pros

  - No redeploy to enable/disable features.
  - Safe rollouts (dark launch, pilot clients, beta users).
  - Instant kill switch for broken modules.
  - Clear audit trail if we keep the logging.

  Cons

  - More operational complexity (admin toggles, governance).
  - Slight latency overhead (cache lookup per check).
  - Requires discipline around clientId sourcing.

  Risk call‑outs

  - If a handler uses the wrong clientId, flags can misfire
    (false deny/allow). Mitigate with routing conventions.
  - Flag sprawl can happen without governance (too many toggles,
    unclear ownership).
  - Multi‑node environment means changes take effect per node
    after TTL.

  Bottom line
  We’re close to build‑ready. The architecture is consistent,
  the feature‑flag plan fits it, and the remaining work is
  implementation + migration. The only real choices are how much
  “enterprise‑grade” logging/override logic we want in Phase 1.


  