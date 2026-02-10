# TASK-010: Feature Flag Read Endpoints (Frontend Support)

> Priority: HIGH — Blocks frontend feature flag admin UI
> Depends on: TASK-009 (complete)
> Estimated scope: 4 new endpoints + 2 new DAC methods + 2 new queries

## Background

The feature flag system (TASK-009) ships 8 endpoints covering CRUD for features, client subscriptions, and user overrides. However, it's missing **read endpoints** for listing client subscriptions and user overrides — the frontend needs these to populate the admin UI's detail tabs.

Additionally, the frontend requests a **feature evaluation debug endpoint** to power a decision tree visualizer.

## Gap Analysis (from frontend AI review)

| Gap | Current State | Needed |
|-----|--------------|--------|
| Client subscriptions by feature | No GET endpoint | `GET /api/v1/features/{featureId}/clients` |
| Client subscriptions by client | No GET endpoint | `GET /api/v1/clients/{clientId}/features` |
| User overrides by feature | No GET endpoint | `GET /api/v1/features/{featureId}/user-overrides` |
| User overrides by user | No GET endpoint | `GET /api/v1/users/{userId}/feature-overrides` |
| Feature evaluation | Internal only (FeatureGate) | `GET /api/v1/features/{featureCode}/evaluate` (optional) |
| Validation error shape | Flat error codes, not field-mapped | Consider `errors` dict (optional) |
| Cascade delete counts | 204 No Content, no counts | Consider returning counts (optional) |

## Required Endpoints (Phase 1 — must have)

### 1. GET /api/v1/features/{featureId}/clients
**Purpose:** List all client subscriptions for a given feature
**Response:** `200 OK`
```json
[
  {
    "id": "...",
    "clientId": "...",
    "clientName": "Acme Corp",
    "clientCode": "ACME",
    "featureId": "...",
    "isEnabled": true,
    "createdAt": "...",
    "updatedAt": "..."
  }
]
```
**Notes:**
- JOIN with clients table to include clientName and clientCode (denormalized for display)
- Filter: `is_deleted = FALSE` on both client_features and clients
- Order by client name ASC
- LIMIT 1000 (safety cap, same as GetAllFeatures)
- Auth: `Permissions(FeatureFlagConstants.Permissions.ListView)`

### 2. GET /api/v1/clients/{clientId}/features
**Purpose:** List all feature subscriptions for a given client
**Response:** `200 OK`
```json
[
  {
    "id": "...",
    "clientId": "...",
    "featureId": "...",
    "featureCode": "DASHBOARD_V2",
    "featureName": "Dashboard V2",
    "isEnabled": true,
    "featureIsActive": true,
    "createdAt": "...",
    "updatedAt": "..."
  }
]
```
**Notes:**
- JOIN with features table for featureCode, featureName, featureIsActive
- Filter: `is_deleted = FALSE` on both client_features and features
- Order by feature code ASC
- Auth: `Permissions(FeatureFlagConstants.Permissions.ListView)`

### 3. GET /api/v1/features/{featureId}/user-overrides
**Purpose:** List all user overrides for a given feature
**Response:** `200 OK`
```json
[
  {
    "id": "...",
    "userId": "...",
    "userDisplayName": "John Doe",
    "userEmail": "john@acme.com",
    "featureId": "...",
    "overrideState": "FORCE_ON",
    "reason": "Beta tester",
    "createdAt": "...",
    "createdBy": "..."
  }
]
```
**Notes:**
- JOIN with users table for userDisplayName, userEmail
- Filter: users.is_active = TRUE (don't show overrides for deactivated users)
- user_feature_overrides has NO soft delete — just filter active users
- Order by user display name ASC
- Auth: `Permissions(FeatureFlagConstants.Permissions.ListView)`

### 4. GET /api/v1/users/{userId}/feature-overrides
**Purpose:** List all feature overrides for a given user
**Response:** `200 OK`
```json
[
  {
    "id": "...",
    "userId": "...",
    "featureId": "...",
    "featureCode": "DASHBOARD_V2",
    "featureName": "Dashboard V2",
    "overrideState": "FORCE_ON",
    "reason": "Beta tester",
    "createdAt": "...",
    "createdBy": "..."
  }
]
```
**Notes:**
- JOIN with features table for featureCode, featureName
- Filter: features.is_deleted = FALSE
- Order by feature code ASC
- Auth: `Permissions(FeatureFlagConstants.Permissions.ListView)`

## Optional Endpoints (Phase 2 — nice to have)

### 5. GET /api/v1/features/{featureCode}/evaluate?clientId={clientId}&userId={userId}
**Purpose:** Debug endpoint — shows the FeatureGate decision path for a specific context
**Response:** `200 OK`
```json
{
  "featureCode": "DASHBOARD_V2",
  "isEnabled": false,
  "reasonCode": "CLIENT_OFF",
  "evaluationPath": [
    { "gate": "GLOBAL", "result": "PASS", "detail": "is_active = true" },
    { "gate": "CLIENT", "result": "FAIL", "detail": "client is_enabled = false" },
    { "gate": "USER", "result": "SKIPPED", "detail": "blocked by CLIENT_OFF" }
  ]
}
```
**Notes:**
- Wraps existing `FeatureGate.IsEnabledAsync()` with added step tracing
- Dev/admin only — could be behind a debug flag or admin-only permission
- Useful for the frontend's decision tree visualizer

## Implementation Plan

### New Files Needed

**Core layer:**
- `ReadModels/ClientFeatureDetailReadModel.cs` — denormalized with client name/code
- `ReadModels/ClientFeatureByClientReadModel.cs` — denormalized with feature code/name
- `ReadModels/UserOverrideDetailReadModel.cs` — denormalized with user name/email
- `ReadModels/UserOverrideByUserReadModel.cs` — denormalized with feature code/name

**Infrastructure layer:**
- `Queries/Features/GetClientSubscriptions.cs` — query + handler
- `Queries/Features/GetClientFeatures.cs` — query + handler
- `Queries/Features/GetFeatureUserOverrides.cs` — query + handler
- `Queries/Features/GetUserOverrides.cs` — query + handler
- Add 4 new methods to `IFeatureReadDac` interface
- Add 4 new methods to `FeatureReadDac` implementation

**API layer:**
- `Endpoints/Features/GetClientSubscriptions/Endpoint.cs`
- `Endpoints/Features/GetClientFeatures/Endpoint.cs`
- `Endpoints/Features/GetFeatureUserOverrides/Endpoint.cs`
- `Endpoints/Features/GetUserOverrides/Endpoint.cs`

**Tests:**
- Unit tests for 4 new handlers
- Integration tests for 4 new DAC methods + 4 new endpoints

### Swarming Strategy
Same pattern as TASK-009 integration tests:
- **Step 0 (sequential):** Read models + interface methods + DAC SQL
- **Step 1 (parallel):** Agent A = queries + handlers + unit tests, Agent B = endpoints + integration tests
- **Step 2:** Verify build + tests

## Frontend AI Questions — Answers to Relay

These answers should be included in the frontend seed prompt update:

1. **Response wrapping:** Raw JSON, no WebApiResult<T> wrapper
2. **Client subscription GETs:** Coming in TASK-010 (this task)
3. **User override GETs:** Coming in TASK-010 (this task)
4. **Client shape:** `{ id, name, code, status, createdAt, updatedAt }`
5. **User shape:** `{ id, displayName, email, contactNumber, isActive, localLoginEnabled, ssoLoginEnabled, ssoProvider, externalId, lastLoginAt, lastLoginProvider, createdAt, createdBy, updatedAt, updatedBy }` — no clientId (users are global, linked to clients via project_assignments)
6. **Feature evaluation endpoint:** Coming in TASK-010 Phase 2 (optional)
7. **CORS:** AllowAll, localhost:8080 works fine
8. **Seeded features:** DASHBOARD_V2, SYS_COMBO_BREAK_DEBUG. Dev admin: `019ac92a-de20-7793-b8df-b88a87ea4e34`
9. **Cascade delete:** 204 No Content, no counts, all backend-side
10. **Validation errors:** Flat semicolon-joined error codes in `detail` field, no field-mapped `errors` dict
11. **Code uniqueness:** No exists endpoint, handle 409
12. **Concurrency:** Last-write-wins, no ETag
13. **requiresClient=false:** Hide client subscriptions tab, backend ignores but doesn't prevent creation
