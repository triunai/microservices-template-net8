# Frontend AI Seed Prompt — Rgt.Space Feature Flag Admin UI

> Copy everything below this line into your frontend AI session.

---

## Context & Role

You are building the **frontend admin UI** for the Rgt.Space Feature Flag system. The backend API is fully built, tested (107/107 green), and production-ready. Your job is to build a React/Next.js (or whatever stack we agree on) admin interface that consumes this API.

Before writing any code, you MUST set up project documentation following the patterns described in the "Documentation System" section below. This is how we track state across sessions.

---

## Documentation System (SET UP FIRST)

The backend team uses a structured documentation system that keeps AI assistants oriented across sessions. You MUST replicate this for the frontend project. Create these files before writing any code:

### 1. `CLAUDE.md` (Project Root)

This is the master context file. It gets loaded into every AI session automatically. It should contain:

```markdown
# [Project Name] — Frontend

## Architecture
- Framework, state management, routing, styling approach
- Directory structure (src/, components/, pages/, hooks/, services/, types/)
- Key libraries and versions

## Tech Stack
| Layer | Technology |
|-------|-----------|
| Framework | React/Next.js/etc |
| State | Zustand/Redux/etc |
| Styling | Tailwind/etc |
| HTTP | Axios/fetch/etc |
| Forms | React Hook Form/etc |
| Tables | TanStack Table/etc |

## Key Conventions
- Component naming (PascalCase, one component per file)
- File structure conventions
- API integration patterns (how services call the backend)
- Error handling patterns (how API errors are displayed)
- Auth token management (where JWT is stored, how it's sent)

## Build & Dev
```bash
npm install
npm run dev    # Start dev server
npm run build  # Production build
npm run test   # Run tests
```

## Known Tech Debt
- List anything you intentionally cut corners on
```

### 2. `READMEs/State/hot-state.md` (Living State Document)

Updated every session. This is the single source of truth for "where are we right now?"

```markdown
# Hot State — [Project Name]

> Living document. Updated each session.

## Current Branch
`feature/feature-flag-admin-ui`

## Project Health
- **Build**: [status]
- **Tests**: [count] total, [pass] passing
- **Lint**: [status]

## What's Done
- [x] Project scaffolding
- [x] Component: FeatureTable
- [ ] Component: FeatureForm
- etc.

## What's In Progress
- Currently working on: [specific thing]
- Blocked by: [if anything]

## NEXT UP
1. [Priority 1]
2. [Priority 2]
3. [Priority 3]

## Pages & Components
| Page/Component | Route | Status |
|---------------|-------|--------|
| Feature List | /admin/features | Done |
| Feature Detail | /admin/features/:id | In Progress |
| etc. | | |
```

### 3. `READMEs/Timeline/YYYY-MM-DD.md` (Per-Session Log)

Create one per session. Documents what happened, decisions made, and lessons learned.

```markdown
# Session: YYYY-MM-DD

## Objective
What we set out to do this session.

## What We Did
1. Built X component
2. Integrated Y endpoint
3. Fixed Z bug

## Decisions Made
- Chose Zustand over Redux because [reason]
- Used server components for [reason]

## Issues Encountered
- [Issue]: [How it was resolved]

## Next Session
- [What to pick up next]
```

### 4. `READMEs/BusinessRules/FEATURE-FLAGS.md` (Domain Knowledge)

Documents the business rules that the UI must enforce/display. This file already exists in the backend — here's the content your frontend needs to know:

```markdown
# Feature Flag Business Rules

## Three-Tier Decision Model
Features are evaluated in this exact order:
1. **Global Gate** — Master kill switch (is_active on the feature itself)
2. **Client Gate** — Client subscription (client_features junction table)
3. **User Override** — Per-user FORCE_ON / FORCE_OFF

## Critical Rule: CLIENT_OFF Blocks Everything
If a client's subscription is disabled (is_enabled = FALSE), NO user override
can bypass it. FORCE_ON at user level CANNOT override CLIENT_OFF.

## Feature Properties
- **Code**: Immutable after creation. UPPER_SNAKE_CASE (e.g., DASHBOARD_V2)
- **Name**: Display name, mutable
- **Description**: Optional admin description
- **IsActive**: Global toggle — FALSE means OFF for everyone
- **RequiresClient**: If TRUE, feature needs client subscription to work.
  If FALSE, it's a system-wide feature (no client gate evaluation)

## Override States
Only two valid values: `FORCE_ON` and `FORCE_OFF`

## Status Values
Only `Active` or `Inactive` for entities across the system.

## Soft Delete Pattern
All features and client subscriptions use soft delete (is_deleted flag).
User overrides use hard delete (row removed entirely).
The UI should NEVER show soft-deleted records.
```

---

## Backend API Reference

### Base URL
```
http://localhost:5000/api/v1
```

### Authentication
Currently `AllowAnonymous()` on all feature flag endpoints (temporary bypass for development). When auth is restored:
- All requests need a JWT Bearer token in `Authorization: Bearer <token>`
- Permissions are RBAC-based: `MODULE.RESOURCE.ACTION` format
- Feature flag permissions: `FEATURE_FLAGS.FEATURES.VIEW`, `FEATURE_FLAGS.FEATURES.EDIT`, etc.

### Error Response Format (RFC 7807 ProblemDetails)
All errors return this shape:
```json
{
  "type": "https://httpstatuses.com/404",
  "title": "Feature Not Found",
  "status": 404,
  "detail": "The requested feature does not exist or has been deleted.",
  "instance": "/api/v1/features/019ac92a-...",
  "correlationId": "abc-123",
  "errorCode": "FEATURE_NOT_FOUND",
  "timestamp": "2026-10-02T09:30:00Z"
}
```

### Error Codes
| Code | HTTP Status | Meaning |
|------|-------------|---------|
| `FEATURE_NOT_FOUND` | 404 | Feature doesn't exist or is soft-deleted |
| `FEATURE_CODE_EXISTS` | 409 | Duplicate feature code |
| `FEATURE_DELETED` | 422 | Concurrent deletion detected |
| `USER_NOT_FOUND` | 404 | User doesn't exist |
| `CLIENT_NOT_FOUND` | 404 | Client doesn't exist |
| `VALIDATION_ERROR` | 400 | Request body failed validation |

---

## Endpoints (Complete Reference)

### 1. GET /api/v1/features
**Purpose:** List all feature flags (admin table view)
**Response:** `200 OK`
```json
[
  {
    "id": "019ac92a-0001-7000-0000-000000000001",
    "code": "DASHBOARD_V2",
    "name": "Dashboard V2",
    "description": "New dashboard experience with real-time metrics",
    "isActive": true,
    "requiresClient": true,
    "createdAt": "2026-10-01T12:00:00Z",
    "createdBy": "019ac92a-de20-7793-b8df-b88a87ea4e34",
    "updatedAt": "2026-10-02T09:00:00Z",
    "updatedBy": "019ac92a-de20-7793-b8df-b88a87ea4e34"
  }
]
```
**Notes:**
- Returns up to 1000 features (server-side LIMIT)
- Ordered alphabetically by code
- Only returns non-deleted features
- UI should show: Code, Name, IsActive (toggle/badge), RequiresClient (badge), last updated

### 2. GET /api/v1/features/{featureId}
**Purpose:** Get single feature detail
**Response:** `200 OK` — same shape as list item above
**Error:** `404` if feature not found or deleted

### 3. POST /api/v1/features
**Purpose:** Create a new feature flag
**Request Body:**
```json
{
  "code": "NEW_REPORTING_ENGINE",
  "name": "New Reporting Engine",
  "description": "Experimental reporting with chart builder",
  "isActive": false,
  "requiresClient": true
}
```
**Validation Rules (enforce in UI before submit):**
- `code`: Required, max 50 chars, UPPER_SNAKE_CASE only (`^[A-Z][A-Z0-9_]*$`), immutable after creation
- `name`: Required, max 255 chars
- `description`: Optional
- `isActive`: Boolean, defaults to FALSE (safe default — new features start OFF)
- `requiresClient`: Boolean, defaults to TRUE
**Response:** `201 Created` with Location header
**Errors:** `400` (validation), `409` (code already exists)

### 4. PUT /api/v1/features/{featureId}
**Purpose:** Update feature properties (full PUT, not partial patch)
**Request Body:**
```json
{
  "name": "Updated Reporting Engine",
  "description": "Now with PDF export",
  "isActive": true,
  "requiresClient": true
}
```
**Note:** Code is NOT in the request — it's immutable. The UI should show the code as read-only.
**Response:** `204 No Content`
**Errors:** `400` (validation), `404` (feature not found or concurrently deleted)

### 5. DELETE /api/v1/features/{featureId}
**Purpose:** Soft-delete a feature (cascades to client subscriptions and user overrides)
**Response:** `204 No Content`
**Errors:** `404` (feature not found or already deleted)
**UI Note:** Should show a confirmation dialog. Explain that deleting a feature also removes all client subscriptions and user overrides for that feature.

### 6. PUT /api/v1/clients/{clientId}/features/{featureId}
**Purpose:** Upsert client feature subscription (enable/disable a feature for a specific client)
**Request Body:**
```json
{
  "isEnabled": true
}
```
**Response:** `204 No Content`
**Errors:** `404` (feature or client not found)
**UI Note:** This is an upsert — if no subscription exists, it creates one. If it exists, it updates the isEnabled flag. The UI should show a simple toggle per client per feature.

### 7. POST /api/v1/users/{userId}/feature-overrides
**Purpose:** Set a user-level override (FORCE_ON or FORCE_OFF)
**Request Body:**
```json
{
  "featureCode": "DASHBOARD_V2",
  "overrideState": "FORCE_ON",
  "reason": "Beta tester for Q4 rollout"
}
```
**Validation Rules:**
- `featureCode`: Required, must exist
- `overrideState`: Required, must be exactly `"FORCE_ON"` or `"FORCE_OFF"`
- `reason`: Optional but recommended (audit trail)
**Response:** `204 No Content`
**Errors:** `400` (validation, invalid override state), `404` (feature or user not found)

### 8. DELETE /api/v1/users/{userId}/feature-overrides/{featureCode}
**Purpose:** Clear (hard-delete) a user's override for a specific feature
**Response:** `204 No Content`
**Errors:** `404` (feature or user not found)
**UI Note:** After clearing, the user falls back to the client-level or global-level decision.

---

## Database Schema (for understanding data relationships)

### Table: features
| Column | Type | Notes |
|--------|------|-------|
| id | UUID (v7) | Primary key, time-ordered |
| code | VARCHAR(50) | UPPER_SNAKE_CASE, unique (partial index on non-deleted), immutable |
| name | VARCHAR(255) | Display name |
| description | TEXT | Optional |
| is_active | BOOLEAN | Global toggle, default FALSE |
| requires_client | BOOLEAN | Client-scoped flag, default TRUE |
| created_at | TIMESTAMP | UTC |
| created_by | UUID | FK to users |
| updated_at | TIMESTAMP | Auto-updated by DB trigger |
| updated_by | UUID | FK to users |
| is_deleted | BOOLEAN | Soft delete flag |

### Table: client_features (junction)
| Column | Type | Notes |
|--------|------|-------|
| id | UUID (v7) | Primary key |
| client_id | UUID | FK to clients (ON DELETE RESTRICT) |
| feature_id | UUID | FK to features (ON DELETE RESTRICT) |
| is_enabled | BOOLEAN | Client-level toggle, default TRUE |
| Audit columns... | | Same pattern as features |
| Soft delete columns... | | Same pattern |

### Table: user_feature_overrides
| Column | Type | Notes |
|--------|------|-------|
| id | UUID (v7) | Primary key |
| user_id | UUID | FK to users (ON DELETE CASCADE) |
| feature_id | UUID | FK to features (ON DELETE RESTRICT) |
| override_state | VARCHAR(20) | CHECK: 'FORCE_ON' or 'FORCE_OFF' |
| reason | TEXT | Optional audit reason |
| created_at | TIMESTAMP | UTC |
| created_by | UUID | FK to users |
| **No soft delete** | | Hard delete only |

---

## Existing Backend Entities (for type generation)

### TypeScript Types (generate from these)

```typescript
// Feature (from GET /api/v1/features)
interface Feature {
  id: string;          // UUID
  code: string;        // UPPER_SNAKE_CASE, immutable
  name: string;
  description: string | null;
  isActive: boolean;   // Global toggle
  requiresClient: boolean;
  createdAt: string;   // ISO 8601
  createdBy: string | null;  // UUID
  updatedAt: string;   // ISO 8601
  updatedBy: string | null;  // UUID
}

// Create Feature Request (POST /api/v1/features)
interface CreateFeatureRequest {
  code: string;           // Required, UPPER_SNAKE_CASE, max 50
  name: string;           // Required, max 255
  description?: string;   // Optional
  isActive?: boolean;     // Default: false
  requiresClient?: boolean; // Default: true
}

// Update Feature Request (PUT /api/v1/features/:id)
interface UpdateFeatureRequest {
  name: string;           // Required, max 255
  description?: string;
  isActive: boolean;      // Required (full PUT)
  requiresClient: boolean; // Required (full PUT)
}

// Upsert Client Feature (PUT /api/v1/clients/:cid/features/:fid)
interface UpsertClientFeatureRequest {
  isEnabled: boolean;
}

// Set User Override (POST /api/v1/users/:uid/feature-overrides)
interface SetUserOverrideRequest {
  featureCode: string;
  overrideState: 'FORCE_ON' | 'FORCE_OFF';
  reason?: string;
}

// Client Feature (from client subscription queries)
interface ClientFeature {
  id: string;
  clientId: string;
  featureId: string;
  isEnabled: boolean;
  createdAt: string;
  createdBy: string | null;
  updatedAt: string;
  updatedBy: string | null;
}

// User Override (from user override queries)
interface UserOverride {
  id: string;
  userId: string;
  featureId: string;
  overrideState: 'FORCE_ON' | 'FORCE_OFF';
  reason: string | null;
  createdAt: string;
  createdBy: string | null;
}

// Feature Decision (runtime evaluation result from FeatureGate)
interface FeatureDecision {
  isEnabled: boolean;
  reasonCode: string;
  featureCode: string;
}

// RFC 7807 ProblemDetails (all error responses)
interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail: string;
  instance: string;
  correlationId: string;
  errorCode: string;
  timestamp: string;
}

// Reason Codes (from FeatureGate decision)
type ReasonCode =
  | 'FEATURE_NOT_FOUND'
  | 'GLOBAL_OFF'
  | 'REQUIRES_CLIENT'
  | 'CLIENT_NOT_SUBSCRIBED'
  | 'CLIENT_OFF'
  | 'USER_FORCE_OFF'
  | 'USER_FORCE_ON'
  | 'GRANTED'
  | 'INVALID_CLIENT_ID';

// Override States
type OverrideState = 'FORCE_ON' | 'FORCE_OFF';

// Permission Constants (for RBAC when auth is restored)
const PERMISSIONS = {
  LIST_VIEW: 'FEATURE_FLAGS.FEATURES.VIEW',
  GLOBAL_EDIT: 'FEATURE_FLAGS.FEATURES.EDIT',
  CLIENT_EDIT: 'FEATURE_FLAGS.CLIENT_FEATURES.EDIT',
  OVERRIDE_INSERT: 'FEATURE_FLAGS.USER_OVERRIDES.INSERT',
  OVERRIDE_DELETE: 'FEATURE_FLAGS.USER_OVERRIDES.DELETE',
} as const;
```

---

## Suggested UI Pages

### Page 1: Feature List (`/admin/features`)
- Table with columns: Code, Name, Active (toggle/badge), Requires Client (badge), Updated At
- Actions: Create button, click row to view detail, delete button with confirmation
- Search/filter by code or name
- Sort by code (default), name, updated date

### Page 2: Feature Detail (`/admin/features/:id`)
- Header: Feature code (read-only badge), name, description
- Edit form: Name, Description, IsActive toggle, RequiresClient toggle
- **Client Subscriptions tab:** Table of clients with toggle for isEnabled per client
- **User Overrides tab:** Table of users with override state, reason, and clear button
- Delete feature button (with cascade warning)

### Page 3: Client Feature Management (`/admin/clients/:clientId/features`)
- Table of all features with toggle for client subscription
- Shows which features the client is subscribed to and their enabled/disabled state

### Page 4: User Override Management (`/admin/users/:userId/feature-overrides`)
- Table of user's overrides with feature code, override state, reason
- Add override form (select feature, select FORCE_ON/FORCE_OFF, enter reason)
- Clear override button per row

---

## Decision Tree Visualization

The UI should help admins understand WHY a feature is on/off for a given user. Consider building a "Feature Evaluator" component that shows the decision path:

```
Feature: DASHBOARD_V2
├── 1. Global Gate: is_active = TRUE ✅
├── 2. Client Gate (Client: Acme Corp):
│   └── client_features: is_enabled = TRUE ✅
├── 3. User Override (User: john@acme.com):
│   └── override_state = FORCE_ON ✅
└── Result: ENABLED (reason: USER_FORCE_ON)
```

Or a failure case:
```
Feature: DASHBOARD_V2
├── 1. Global Gate: is_active = TRUE ✅
├── 2. Client Gate (Client: Beta Inc):
│   └── client_features: is_enabled = FALSE ❌ ← BLOCKED HERE
│   └── ⚠️ User override FORCE_ON CANNOT bypass CLIENT_OFF
└── Result: DISABLED (reason: CLIENT_OFF)
```

---

## Backend System Context (Other APIs Available)

The backend has other endpoints your UI may need:

| Domain | Key Endpoints | Purpose |
|--------|--------------|---------|
| **Identity** | GET /api/v1/users, GET /api/v1/users/:id | User lookup for override management |
| **Portal Routing** | GET /api/v1/clients, GET /api/v1/clients/:id | Client lookup for subscription management |
| **Auth** | POST /api/v1/auth/login | Local JWT login (returns access + refresh tokens) |
| **Roles** | GET /api/v1/roles | RBAC role lookup |
| **Health** | GET /health, GET /health/live, GET /health/ready | Backend health checks |

---

## Important Backend Gotchas for Frontend

1. **IDs are UUIDv7** — time-ordered, looks like `019ac92a-0001-7000-0000-000000000001`. Always use string type, never parse as number.

2. **Code is immutable** — once a feature is created, the code cannot be changed. The update endpoint does NOT accept a code field. Show it as read-only in edit forms.

3. **Soft delete is invisible** — the API never returns soft-deleted records. You don't need to filter on the frontend.

4. **Full PUT, not PATCH** — the update endpoint requires ALL fields (name, description, isActive, requiresClient). If you only send `isActive`, the other fields will be set to their request values (potentially null/default). Always send the complete object.

5. **Cascade on delete** — deleting a feature removes all client subscriptions and user overrides. The UI MUST warn the user about this.

6. **FORCE_ON cannot bypass CLIENT_OFF** — this is the most important business rule. If a client's subscription is disabled, no user override can enable the feature for users in that client. The UI should make this clear (e.g., greyed out overrides when client is OFF).

7. **No pagination yet** — GetAllFeatures returns up to 1000 rows with no offset/cursor. For now, client-side pagination/filtering is fine. If you need server-side pagination, that's a backend enhancement request.

8. **Timestamps are UTC** — all `createdAt`, `updatedAt` values are UTC. Convert to `Asia/Kuala_Lumpur` for display (the business operates in Malaysia).

9. **Auth is currently bypassed** — all endpoints accept anonymous requests. When auth is restored, you'll need to send JWT tokens. Plan your auth integration now even if you don't need it yet.

10. **Error responses are consistent** — every error is RFC 7807 ProblemDetails with an `errorCode` field. Use `errorCode` for programmatic handling, `detail` for user-facing messages.
