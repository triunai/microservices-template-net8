# TASK-011: Feature Flag Evaluation Endpoints

> Priority: HIGH — Blocks frontend feature flag consumption (sidebar gating, route guards, conditional UI)
> Depends on: TASK-009 (complete), TASK-010 (complete)
> Estimated scope: 2 endpoints, 1 shared evaluator, 2 new DAC methods, refactor FeatureGate, ~14-18 new tests

## Background

The feature flag admin UI is complete (TASK-009 CRUD + TASK-010 read endpoints). Admins can create flags, subscribe clients, and set user overrides. But the application has **no way to evaluate flags at runtime** — the `FeatureGate` service is internal-only with no HTTP surface.

The frontend needs evaluation endpoints to:
- Fetch all feature flag states on login (bulk) and cache them client-side
- Check individual flags ad-hoc (single)
- Show debugging info (reason codes explaining WHY a flag resolved to on/off)

## What Exists Today

`IFeatureGate` + `FeatureGate` already implement the full three-tier decision tree with per-key caching (60s TTL) and stampede protection. 21 unit tests cover every branch. The evaluation logic is proven — we just need HTTP endpoints to expose it, plus a bulk-optimized path.

### Existing Decision Tree (FeatureGate.IsEnabledAsync)

```
1. Feature lookup → FEATURE_NOT_FOUND
2. Global gate (is_active) → GLOBAL_OFF
3. Client gate (requires_client + client_features) → INVALID_CLIENT_ID / CLIENT_NOT_SUBSCRIBED / CLIENT_OFF
4. User override → USER_FORCE_ON / USER_FORCE_OFF
5. All gates passed → GRANTED
```

### Existing Reason Codes (FeatureFlagConstants.ReasonCodes)

```
FEATURE_NOT_FOUND, GLOBAL_OFF, REQUIRES_CLIENT, CLIENT_NOT_SUBSCRIBED,
CLIENT_OFF, USER_FORCE_OFF, USER_FORCE_ON, GRANTED, INVALID_CLIENT_ID
```

---

## Design Decisions (from brainstorm session 2026-02-10)

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Architecture | Hybrid: FeatureGate for single, bulk SQL + shared evaluator for bulk | Single uses proven caching. Bulk does 3 queries instead of 3N+1. |
| Parameter source | All query params (`?userId=&clientId=`) | Boring, debuggable, testable. No JWT coupling in evaluation logic. Auth gates access, params feed evaluation. |
| Shared evaluator | Extract pure static `FeatureEvaluator.Evaluate()` | Single source of truth. Both FeatureGate and bulk handler use it. Pure function = trivially testable. |
| GRANTED split | Split into `GRANTED_SYSTEM` + `GRANTED_CLIENT` | Tells frontend WHY access was granted, not just that it was. |
| clientId handling | Optional on both endpoints | System-wide features evaluate without clientId. Client-scoped features return INVALID_CLIENT_ID if clientId missing. |
| Response format | Raw JSON (no WebApiResult wrapper) | Matches all existing feature flag endpoints. |
| Non-existent userId/clientId | 200 with features evaluated (no overrides/subscriptions = default) | Evaluation is a pure function of data state, not entity existence. |

---

## Reason Code Catalog (10 total)

| Code | enabled | Tier | Meaning |
|------|---------|------|---------|
| `FEATURE_NOT_FOUND` | false | 0 | Feature code doesn't exist (or soft-deleted) |
| `GLOBAL_OFF` | false | 1 | Feature exists but `is_active = false` |
| `INVALID_CLIENT_ID` | false | 2 | Client-scoped feature but clientId was empty/missing |
| `REQUIRES_CLIENT` | false | 2 | Server-side only: called `IsEnabledGlobally` on client-scoped feature |
| `CLIENT_NOT_SUBSCRIBED` | false | 2 | No `client_features` row for this client+feature |
| `CLIENT_OFF` | false | 2 | Client subscription exists but `is_enabled = false` |
| `USER_FORCE_OFF` | false | 3 | User override is `FORCE_OFF` |
| `USER_FORCE_ON` | true | 3 | User override is `FORCE_ON` |
| `GRANTED_SYSTEM` | true | — | System-wide feature, all gates passed, no override |
| `GRANTED_CLIENT` | true | — | Client-scoped feature, subscription enabled, no override |

Note: `REQUIRES_CLIENT` is server-side only (used by `IsEnabledGloballyAsync`). It will not appear in HTTP endpoint responses.

---

## Endpoints

### 1. GET /api/v1/features/evaluate/{featureCode}

**Purpose:** Evaluate a single feature for a given user + client context.

**Route:** `GET /api/v1/features/evaluate/{featureCode}?userId={guid}&clientId={guid}`

**Parameters:**
| Param | Source | Required | Notes |
|-------|--------|----------|-------|
| `featureCode` | Route | Yes | Case-insensitive, normalized to UPPER |
| `userId` | Query | Yes | The user to evaluate for |
| `clientId` | Query | No | Required for client-scoped features. If omitted, client-scoped features return `INVALID_CLIENT_ID`. |

**Response:** `200 OK`
```json
{
  "featureCode": "PORTAL_ROUTING",
  "isEnabled": true,
  "reasonCode": "GRANTED_CLIENT"
}
```

**Implementation:** MediatR query → calls `IFeatureGate.IsEnabledAsync(featureCode, clientId, userId)` → returns `FeatureDecision` as JSON.

**HTTP Semantics:**
| Scenario | Status |
|----------|--------|
| Normal evaluation | 200 |
| Missing userId | 400 (validation error) |
| Feature doesn't exist | 200 with `FEATURE_NOT_FOUND` (not 404) |

**Auth:** `Permissions(FeatureFlagConstants.Permissions.EvaluateView)` — new permission constant.

---

### 2. GET /api/v1/features/evaluate

**Purpose:** Evaluate ALL features for a given user + client context. Called once on frontend login.

**Route:** `GET /api/v1/features/evaluate?userId={guid}&clientId={guid}`

**Parameters:**
| Param | Source | Required | Notes |
|-------|--------|----------|-------|
| `userId` | Query | Yes | The user to evaluate for |
| `clientId` | Query | No | If omitted, client-scoped features return `INVALID_CLIENT_ID` |

**Response:** `200 OK`
```json
{
  "userId": "019ac92a-de20-7793-b8df-b88a87ea4e34",
  "clientId": "019ac92a-0002-7000-0000-000000000002",
  "evaluatedAt": "2026-02-11T10:30:00Z",
  "features": [
    { "featureCode": "PORTAL_ROUTING", "isEnabled": true, "reasonCode": "GRANTED_CLIENT" },
    { "featureCode": "SYS_COMBO_BREAK_DEBUG", "isEnabled": false, "reasonCode": "GLOBAL_OFF" },
    { "featureCode": "USER_MANAGEMENT", "isEnabled": true, "reasonCode": "GRANTED_SYSTEM" }
  ]
}
```

**Implementation:** MediatR query → 3 bulk DAC queries → loop with `FeatureEvaluator.Evaluate()` per feature.

**HTTP Semantics:**
| Scenario | Status |
|----------|--------|
| Normal evaluation | 200 |
| Zero features in system | 200 with `{ features: [] }` |
| Missing userId | 400 (validation error) |
| Non-existent userId/clientId | 200 (features evaluated with defaults) |

**Auth:** `Permissions(FeatureFlagConstants.Permissions.EvaluateView)` — same permission as single eval.

---

## Implementation Phases

### Phase 1: Extract Shared Evaluator (Refactor — no new functionality)

**Goal:** Extract decision tree from `FeatureGate` into pure static `FeatureEvaluator`. All 21 existing tests must still pass.

**Files Modified:**
- `Infrastructure/Services/Features/FeatureEvaluator.cs` — NEW: pure static evaluator
- `Infrastructure/Services/Features/FeatureGate.cs` — MODIFIED: delegates to FeatureEvaluator
- `Core/Constants/FeatureFlagConstants.cs` — MODIFIED: add `GRANTED_SYSTEM`, `GRANTED_CLIENT`, `EvaluateView` permission
- `Tests/Unit/Services/FeatureGateTests.cs` — MODIFIED: update 3-4 assertions for GRANTED split

**Verification:** Build 0 errors, all 21 FeatureGate tests green, no behavior change.

### Phase 2: New DAC Methods + Evaluator Unit Tests

**Goal:** Add bulk DAC methods and comprehensive evaluator tests.

**New DAC Methods on `IFeatureReadDac` / `FeatureReadDac`:**

```csharp
/// All client subscriptions for a client (no JOIN, just client_features rows).
Task<IReadOnlyList<ClientFeatureReadModel>> GetClientFeaturesByClientAsync(Guid clientId, CancellationToken ct);

/// All user overrides for a user (no JOIN, just user_feature_overrides rows).
Task<IReadOnlyList<UserOverrideReadModel>> GetUserOverridesByUserAsync(Guid userId, CancellationToken ct);
```

**SQL (no JOINs — evaluator only needs is_enabled / override_state):**

```sql
-- GetClientFeaturesByClientAsync
SELECT id, client_id, feature_id, is_enabled, created_at, created_by, updated_at, updated_by
FROM client_features
WHERE client_id = @ClientId AND is_deleted = FALSE;

-- GetUserOverridesByUserAsync
SELECT id, user_id, feature_id, override_state, reason, created_at, created_by
FROM user_feature_overrides
WHERE user_id = @UserId;
```

**New Tests:**
- `Tests/Unit/Services/FeatureEvaluatorTests.cs` — ~10 tests: one per reason code (pure function, no mocks)
- `Tests/Integration/Persistence/FeatureDacIntegrationTests.cs` — +2 tests: bulk DAC methods

**Verification:** Build 0 errors, all existing + new tests green.

### Phase 3: Endpoints + Handler + Integration Tests

**Goal:** Wire up the 2 HTTP endpoints with MediatR handlers.

**New Files:**
- `Infrastructure/Queries/Features/EvaluateFeature.cs` — single eval query + handler
- `Infrastructure/Queries/Features/BulkEvaluateFeatures.cs` — bulk eval query + handler
- `API/Endpoints/Features/EvaluateFeature/Endpoint.cs` — single eval endpoint
- `API/Endpoints/Features/BulkEvaluateFeatures/Endpoint.cs` — bulk eval endpoint

**Response DTOs (nested in query files or separate):**
- `BulkEvaluationResponse` — wraps `userId`, `clientId`, `evaluatedAt`, `features[]`

**New Tests:**
- `Tests/Unit/Handlers/Features/EvaluateFeatureHandlerTests.cs` — single eval handler unit tests
- `Tests/Unit/Handlers/Features/BulkEvaluateFeaturesHandlerTests.cs` — bulk eval handler unit tests
- `Tests/Integration/Api/FeatureEndpointTests.cs` — +2-4 endpoint integration tests

**Verification:** Full build + full test suite green.

---

## Bulk Evaluation Data Flow

```
Frontend login
  → GET /api/v1/features/evaluate?userId=X&clientId=Y
    → BulkEvaluateFeatures.Handler
      → 3 parallel-safe DAC calls:
        1. IFeatureReadDac.GetAllAsync()                          → List<FeatureReadModel>
        2. IFeatureReadDac.GetClientFeaturesByClientAsync(clientId) → List<ClientFeatureReadModel>
        3. IFeatureReadDac.GetUserOverridesByUserAsync(userId)     → List<UserOverrideReadModel>
      → For each feature:
        - Lookup client sub by feature.Id (dictionary)
        - Lookup user override by feature.Id (dictionary)
        - FeatureEvaluator.Evaluate(feature, code, clientId, clientSub, userOverride)
      → Collect FeatureDecision[]
    → 200 OK { userId, clientId, evaluatedAt, features: [...] }
```

---

## Critical Truth Table Row (Inherited from TASK-009)

**CLIENT_OFF blocks USER_FORCE_ON.** A disabled client subscription overrides any user-level FORCE_ON. This is truth table row 9 from the original spec:

| Global | Client | User Override | Result | Reason |
|--------|--------|--------------|--------|--------|
| ON | OFF | FORCE_ON | **OFF** | `CLIENT_OFF` |

The `FeatureEvaluator` inherits this from the existing `FeatureGate` logic. Test for it explicitly.

---

## Known Architectural Notes

1. **Bulk is always-fresh, single is cached.** The bulk endpoint does 3 fresh DB queries. The single endpoint uses FeatureGate's 60s per-key cache. There's a window where they can return different results. This is pre-existing cache behavior, not a bug.

2. **Short-circuit consistency.** FeatureGate may skip fetching client/user data when it knows the answer early (global off). The evaluator handles null inputs for those fields correctly — `null clientFeature + requiresClient = CLIENT_NOT_SUBSCRIBED`.

3. **Refactor before extend.** Phase 1 is a pure refactor (extract evaluator). All 21 existing tests must pass before any new functionality is added. This prevents regression.

4. **REQUIRES_CLIENT stays server-side.** The `IsEnabledGloballyAsync` method and its `REQUIRES_CLIENT` reason code are for server-side use only. The HTTP endpoints always use `IsEnabledAsync` / bulk evaluation path, which returns `INVALID_CLIENT_ID` instead.

---

## Swarming Guidance

**Phase 1:** Sequential only (refactoring shared code).

**Phase 2:** Can swarm (2 agents):
- Agent A: FeatureEvaluator unit tests
- Agent B: DAC integration tests

**Phase 3:** Can swarm (2 agents):
- Agent A: Query + handler files + unit tests
- Agent B: Endpoint files + endpoint integration tests

Max 2 agents per step. Each agent owns non-overlapping files.

---

## Estimated Test Counts

| Category | Before | After | Delta |
|----------|--------|-------|-------|
| Unit (evaluator) | 0 | ~10 | +10 |
| Unit (handlers) | 0 | ~4 | +4 |
| Unit (FeatureGate) | 21 | 21 | 0 (assertions updated) |
| Integration (DAC) | 20 | 22 | +2 |
| Integration (endpoints) | 18 | 20-22 | +2-4 |
| **Total** | **123** | **~139-145** | **+16-22** |
