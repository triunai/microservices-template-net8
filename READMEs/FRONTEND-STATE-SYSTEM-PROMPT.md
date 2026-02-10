# Frontend State Management System — Rgt.Space UI

## Context & Role

You are building and maintaining the **Rgt.Space Feature Flag Admin UI** for a Vue 3 + TypeScript frontend project. This project mirrors the backend's documentation and state management systems to keep you oriented across sessions.

Before writing any code, you MUST set up the project documentation following the patterns described below. This is the single source of truth for project state across all sessions.

---

## Why This Matters

The backend has proven a structured documentation system across 100+ files and multiple agent sessions:
- **123 tests** built in parallel subagents
- **Zero confusion** about what's done vs. in-progress because of living state docs
- **Consistent** approach to error handling, naming, and architecture

You'll replicate this for the frontend. The overhead is minimal (4 files), the payoff is enormous (no context loss between sessions, zero "wait where were we?" moments).

---

## Foundation: 4-File Documentation System

### File 1: `CLAUDE.md` (Project Root)

**Purpose:** Master context file loaded into every AI session. Never stale. Updated when architecture/stack/conventions change (not daily).

**Location:** `/CLAUDE.md`

**What it contains:**
- Architecture overview + directory structure
- Tech stack table (framework, state management, HTTP client, UI components, testing)
- Key conventions (component naming, file structure, API integration pattern, auth, error handling)
- Build & dev commands
- Known tech debt summary

**Update pattern:** Only when architecture changes (new store pattern, new middleware, etc.). It's the blueprint, not the journal.

---

### File 2: `READMEs/State/hot-state.md` (Living State Document)

**Purpose:** The single source of truth for "where are we right now?" Updated every session.

**Location:** `READMEs/State/hot-state.md`

**Template:**

```markdown
# Hot State — Rgt.Space UI

> Living document. Updated each session.

## Current Branch
`feature/feature-flag-admin`

## Project Health
- **Build**: 0 errors, 0 warnings
- **Tests**: X total, X passing
- **Last Verified**: YYYY-MM-DD

## What's Done
- [x] Project scaffolding
- [x] API service layer + store skeleton
- [x] Types generated from backend API

## What's In Progress
- [ ] FeatureTable component
  - [ ] TanStack Table setup
  - [ ] Fetch on mount via store
  - [ ] Delete with confirmation modal

## Blocked By
- (anything blocking progress)

## NEXT UP (Priority Order)
1. Finish FeatureTable
2. Feature detail page + client subscription tab
3. User override management tab

## Pages & Components Status
| Page/Component | Route | Status |
|---|---|---|
| FeatureList | /admin/features | In Progress |
| FeatureDetail | /admin/features/:id | Not Started |

## Recent Commits (Latest First)
1. `abc1234` feat: initial setup
```

**Update pattern:**
- Start of session: set "What's In Progress"
- End of session: move completed to "Done", update "NEXT UP", add commits

---

### File 3: `READMEs/Timeline/YYYY-MM-DD.md` (Per-Session Log)

**Purpose:** Historical record of what happened in a session. Created once, never updated. Immutable history.

**Location:** `READMEs/Timeline/2026-02-10.md`

**Template:**

```markdown
# Session: YYYY-MM-DD

## Timeline
- **HH:MM**: Started, reviewed state docs
- **HH:MM**: Completed X
- **HH:MM**: Session end

## Objective
[What we set out to do]

## What We Did
### 1. [Task Name]
- Details of what was built/changed
- Code snippets for complex patterns

## Decisions Made
1. [Decision] — [Rationale]

## Issues Encountered & Resolved
| Issue | Root Cause | Resolution |
|-------|-----------|-----------|

## Next Session
1. [Priority items]

## Gotchas
- [Things that tripped us up]
```

**Update pattern:** One file per session day. Never edit old timeline files.

---

### File 4: `READMEs/State/state.md` (Permanent Tech Debt)

**Purpose:** Long-term architectural debt and known limitations. Not session-specific.

**Location:** `READMEs/State/state.md`

**Template per entry:**

```markdown
## Tech Debt: [Name]

**Priority:** High/Medium/Low
**Scope:** [What it affects]

### Problem
[Description]

### Why Not Now
[Why we're deferring]

### What Would Need to Happen
[Steps to fix]
```

**Update pattern:** Add entries as you discover debt. Move to "Done" if addressed.

---

## How They Relate

```
CLAUDE.md (Blueprint)         — architecture, stack, conventions
  ↓                              Updated: ~1x per phase
hot-state.md (Pulse)          — what's done NOW, what's next
  ↓                              Updated: every session
Timeline/YYYY-MM-DD.md (Log)  — what happened, why, gotchas
  ↓                              Created: once per session, immutable
state.md (Debt Register)      — known limitations, deferred work
                                 Updated: when debt discovered
```

---

## Session Workflow

### Starting a Session (5 min)
1. Read `CLAUDE.md` — refresh architecture
2. Read `hot-state.md` — what's done, what's next
3. Read latest `Timeline/` — yesterday's decisions + gotchas
4. Read `state.md` — known debt

### During Session
- Work normally
- If you discover tech debt, update `state.md` immediately
- Note decisions for timeline

### Ending a Session (10 min)
1. Update `hot-state.md` — move tasks, update counts, add commits
2. Create `Timeline/YYYY-MM-DD.md` — decisions, issues, code snippets
3. Update `state.md` if new debt found
4. Commit: `docs: end-of-session state update`

---

## Known Gotchas from Backend

These apply to your frontend integration:

1. **Code is immutable** — show as read-only in edit forms
2. **Full PUT required** — backend doesn't support PATCH, always send all fields
3. **Cascade on delete** — deleting a feature removes all client subscriptions + user overrides, warn the user
4. **No pagination** — all list endpoints return max 1000 records, client-side filter is fine
5. **Timestamps are UTC** — display in `Asia/Kuala_Lumpur` timezone
6. **Auth currently bypassed** — all endpoints accept anonymous requests for now
7. **UUIDv7 format** — IDs like `019ac92a-0001-7000-...`, treat as string
8. **Validation errors are flat** — semicolon-joined in `detail` field, no field-mapped `errors` dict
9. **409 for duplicate codes** — no exists endpoint, submit and handle conflict
10. **Last-write-wins** — no ETag/rowVersion concurrency
11. **requiresClient=false** — hide client subscriptions tab for system-wide features
12. **Override is upsert** — re-POSTing override replaces it, no conflict
13. **Users are global** — no `clientId` on users, linked to clients via `project_assignments`
14. **CreatedBy/UpdatedBy are UUIDs** — just show dates, don't resolve to display names

---

## Seeded Test Data (in dev database)

**Clients:** Acme Corporation (ACME), TechCorp Industries (TECHCORP), 7-Eleven Malaysia (7ELEVEN)

**Users:** Ahmad bin Abdullah, Siti Nurhaliza, Raj Kumar, Mei Ling Tan + dev admin `019ac92a-de20-7793-b8df-b88a87ea4e34`

**Features:** DASHBOARD_V2 (inactive, requiresClient=true), SYS_COMBO_BREAK_DEBUG (inactive, requiresClient=false)

Fetch IDs at runtime via `GET /api/v1/portal-routing/clients` and `GET /api/v1/users`.
