# Combo-Break Debugger: Business Rules & Architecture

**Version:** 2.0  
**Date:** 2026-01-19  
**Status:** ACTIVE (Phase 1-3 Complete)  
**Task Reference:** [TASK-006-Combo-Break-Debugger.md](../Tasks/TASK-006-Combo-Break-Debugger.md)

---

## 🎯 Objective

Provide a low-friction debugging system that treats API requests like "fighting game combos." When a request fails, the system instantly reveals:
- **Where it broke** (`checkpoint.current`)
- **How far it got** (`checkpoint.last`)
- **Full request context** (CorrelationId, TraceId, Route)
- **Step positioning** (`ComboPosition`: "Step 2/5, 3 remaining") — Phase 3

**Mental Model:** Debugging should move from "A→Z guessing" to "start at the break point."

---

## 📜 Core Business Rules

### 1. Checkpoint Semantics (Immutable Contract)

| Field | Meaning | Mutation Rule |
|-------|---------|---------------|
| `checkpoint.current` | The step being attempted at failure time | Set on `Enter()`, preserved on `Fail()` |
| `checkpoint.last` | Last **successfully completed** step | Set on `Complete()`, **NEVER** mutated on `Fail()` |

#### Rule 1.1: No Coalescing
- If `checkpoint.current` is `null`, it means failure occurred **before** any tracked step (e.g., in middleware).
- Do NOT coalesce: `Current ?? Last` is forbidden. Nulls are valuable signals.

#### Rule 1.2: Last Success is Immutable
- `checkpoint.last` represents the last **known good** state.
- On failure, `Last` must remain unchanged — it is the "last hit that connected."

#### Rule 1.3: Stack Always Balanced
- `Enter()` **always** pushes to the stack (never skips, even at MaxStackDepth).
- This ensures `Complete()`/`Fail()` never pops the wrong parent.
- Telemetry is bounded (Activity tags capped), but stack integrity is preserved.

---

### 2. Tracking Layers

#### Layer 1: Automatic Handler Tracking (Zero Effort)
- **Mechanism:** `CheckpointPipelineBehavior` (MediatR Pipeline)
- **Trigger:** Every `_mediator.Send()` call
- **Output:** `checkpoint.current = handler:{HandlerName}`
- **Developer Effort:** None. Automatically wired via DI.

#### Layer 2: Granular Step Tracking (Opt-In)
- **Mechanism:** `ICheckpointTracker.InStep()`
- **Trigger:** Developer wraps code blocks manually
- **Output:** `checkpoint.current = step:{StepName}`
- **Developer Effort:** Inject `ICheckpointTracker`, wrap critical steps.

**When to Use Layer 2:**
- Complex handlers with 5+ sequential operations
- Critical business flows (payments, order processing)
- Handlers with external service calls (DB, HTTP, Queues)

#### Layer 3: Combo Positioning (Phase 3)
- **Mechanism:** `IComboMapProvider` + `ComboPositionCalculator`
- **Trigger:** When a combo is declared for a handler
- **Output:** `ComboPosition` with "Step X of Y, next: Z"
- **Developer Effort:** Declare combos in `AppComboMapProvider`.

---

### 3. Environment-Based Behavior

| Feature | Development | Production |
|---------|-------------|------------|
| `X-Checkpoint-*` Headers | ✅ Added to 5xx responses | ❌ Stripped (security) |
| `checkpointCurrent` in ProblemDetails | ✅ Included | ✅ Included (via tracker) |
| Log: "💥 COMBO BREAK" | ✅ Written | ✅ Written |
| In-Memory Recorder | ✅ Active (ComboBreakRecorder) | ❌ NullRecorder (zero memory) |
| `/_debug/combos` Endpoints | ✅ Available | ❌ Returns 403 Forbidden |
| ComboPosition Calculation | ✅ ExampleComboMapProvider | ❌ NullComboMapProvider |

**Rationale:** Debug telemetry is for developers. Production responses must not leak internal structure to attackers.

---

### 4. Checkpoint Naming Convention

| Layer | Format | Example |
|-------|--------|---------|
| Handler entry (automatic) | `handler:{HandlerName}` | `handler:CreateOrderCommand` |
| Business step | `step:{StepName}` | `step:ValidateInput` |
| Repository call | `repo:{Operation}` | `repo:SaveOrder` |
| External service | `ext:{Service}:{Operation}` | `ext:Stripe:ChargeCard` |
| Domain service | `domain:{Service}:{Method}` | `domain:PricingEngine:Calculate` |

**Guardrail:** Checkpoint names must be **LOW-CARDINALITY**. Never include:
- User data (`step:ProcessUser_john@example.com` ❌)
- GUIDs (`step:ProcessOrder_abc-123` ❌)
- Timestamps (`step:Process_2026-01-12` ❌)

---

### 5. PII & Security Guardrails

| Data Type | Allowed in Checkpoint | Allowed in Logs | Allowed in Snapshot |
|-----------|----------------------|-----------------|---------------------|
| Handler Name | ✅ | ✅ | ✅ |
| Step Name | ✅ | ✅ | ✅ |
| CorrelationId | ✅ | ✅ | ✅ |
| TraceId | ✅ | ✅ | ✅ |
| UserId (GUID only) | ✅ | ✅ | ✅ |
| Route Template | ✅ | ✅ | ✅ |
| Raw Path with GUIDs | ❌ | ✅ (logs only) | ❌ |
| Email / Name | ❌ | ❌ | ❌ |
| JWT Tokens | ❌ | ❌ | ❌ |
| Request Body | ❌ | ❌ | ❌ |
| SQL Parameters | ❌ | ❌ | ❌ |

---

### 6. Parallel Execution Constraint

**The stack model does NOT support parallel InStep() calls.**

```csharp
// ❌ FORBIDDEN
await Task.WhenAll(
    _tracker.InStep("step:A", ...),
    _tracker.InStep("step:B", ...)
);
```

**Reason:** Two steps active simultaneously → undefined "Current" state.

**Rule:** `InStep()` must only be used for **sequential** operations.

---

### 7. Recording Gate (Phase 2)

**Which errors are recorded in the in-memory black box?**

| Error Type | Recorded? | Rationale |
|------------|-----------|-----------|
| 5xx (System Errors) | ✅ Always | System errors are always interesting |
| Validation Errors (400) | ❌ Skip | Routine, high volume |
| Not Found (404) | ❌ Skip | Routine, expected |
| Unexpected 4xx (403, 409, 422) | ✅ Record | Often indicate bugs or security issues |
| AppException subclasses | ❌ Skip | Handled business exceptions |

**Implementation:** Uses `ErrorCatalog.IsRecordableError(errorCode)`.

---

### 8. Combo Position Calculation (Phase 3)

**Step Extraction:**
- `step:ValidateInput:exception` → `ValidateInput`
- Prefixes (`step:`, `repo:`, `ext:`, `handler:`) are stripped
- Suffixes (`:exception`, `:failed`) are stripped
- Only the core step name is matched

**Matching Strategy:**
- Exact match (case-insensitive) against declared combo
- No fuzzy matching, no contains checks
- If step is not in combo: `IsUnknownStep = true`

**Combo Declaration Pattern:**
```csharp
public sealed class AppComboMapProvider : IComboMapProvider
{
    private static readonly Dictionary<string, IReadOnlyList<string>> _combos = new()
    {
        ["CreateBookingCommand"] = new[] { "ValidateInput", "LoadEntity", "CheckPermission", "SaveToDb" },
    };
    // ...
}
```

---

## 🔄 Data Flow

```
┌─────────────────────────────────────────────────────────────────────┐
│                          HTTP Request                               │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  CorrelationIdMiddleware → Assigns CorrelationId                    │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  ComboBreakHeadersMiddleware → Registers OnStarting callback        │
│    → Reads ICheckpointTracker directly (not HttpContext.Items)      │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  FastEndpoints → Calls MediatR.Send()                               │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  CheckpointPipelineBehavior                                         │
│    → Marks: checkpoint.current = handler:{Name}                     │
│    → Calls Handler                                                  │
│    → On Success: Complete() → Last = handler:{Name}                 │
│    → On Failure: Fail() → Current preserved, Last immutable         │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  Handler (with optional InStep calls)                               │
│    → _tracker.InStep("step:A", ...) → Current = step:A              │
│    → Complete → Last = step:A, Current = (parent or null)           │
│    → _tracker.InStep("step:B", ...) → Current = step:B              │
│    → THROWS! → Fail() → Current = step:B, Last = step:A             │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  GlobalExceptionHandler                                             │
│    → Reads ICheckpointTracker.Current, .Last                        │
│    → Logs: "💥 COMBO BREAK | Current: step:B | Last: step:A"        │
│    → Creates ProblemDetails with checkpointCurrent, checkpointLast  │
│    → Phase 2: RecordComboBreak() if IsRecordableError               │
│    → Phase 3: ComboPositionCalculator.Calculate() → ComboPosition   │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│  ComboBreakHeadersMiddleware (OnStarting callback)                  │
│    → If Dev + 5xx: Adds X-Checkpoint-Current, X-Checkpoint-Last     │
│    → Reads ICheckpointTracker directly (works for Result.Fail too)  │
└───────────────────────────────┬─────────────────────────────────────┘
                                ↓
┌───────────────────────────────┴─────────────────────────────────────┐
│                          HTTP Response                              │
│    Headers: X-Checkpoint-Current: step:B                            │
│             X-Checkpoint-Last: step:A                               │
│    Body: ProblemDetails JSON with checkpoint extensions             │
└─────────────────────────────────────────────────────────────────────┘
```

---

## 🛡️ API Contract

### Debug Headers (Dev Only)

| Header | Description |
|--------|-------------|
| `X-Checkpoint-Current` | The step where failure occurred |
| `X-Checkpoint-Last` | The last successfully completed step |
| `X-Trace-Id` | OpenTelemetry Trace ID (if Activity exists) |

### ProblemDetails Extensions

```json
{
  "type": "https://api.errors/INTERNAL_ERROR",
  "title": "Internal Server Error",
  "status": 500,
  "checkpointCurrent": "step:SaveToDb",
  "checkpointLast": "step:ValidateInput",
  "correlationId": "abc-123",
  "traceId": "..."
}
```

### Debug Endpoints (Phase 2)

| Endpoint | Description | Auth |
|----------|-------------|------|
| `GET /_debug/combos` | List all recorded combo breaks | Dev only (403 in prod) |
| `GET /_debug/combos/{correlationId}` | Get specific snapshot | Dev only (403 in prod) |

### ComboBreakSnapshot (Phase 2+3)

```json
{
  "correlationId": "abc-123",
  "timestamp": "2026-01-19T09:00:00Z",
  "checkpointCurrent": "step:SaveToDb",
  "checkpointLast": "step:ValidateInput",
  "traceId": "...",
  "spanId": "...",
  "route": "/api/v1/bookings",
  "handler": "CreateBookingCommand",
  "errorCode": "INTERNAL_ERROR",
  "errorMessage": "Database connection failed",
  "tenantId": "tenant-123",
  "userId": "user-guid",
  "comboPosition": {
    "isDeclared": true,
    "isUnknownStep": false,
    "currentIndex": 3,
    "totalSteps": 5,
    "remaining": 1,
    "currentStep": "SaveToDb",
    "nextStep": "PublishEvent",
    "completedSteps": ["ValidateInput", "LoadEntity", "CheckPermission"],
    "remainingSteps": ["PublishEvent"]
  }
}
```

---

## 📁 Implementation Files

| Layer | File | Purpose |
|-------|------|---------|
| **Core (Contract)** | `Core/Abstractions/Debugging/ICheckpointTracker.cs` | Interface definition |
| **Core (Contract)** | `Core/Abstractions/Debugging/IComboBreakRecorder.cs` | Recorder interface |
| **Core (Contract)** | `Core/Abstractions/Debugging/IComboMapProvider.cs` | Combo map interface |
| **Core (DTO)** | `Core/Debugging/ComboBreakSnapshot.cs` | Snapshot record |
| **Core (DTO)** | `Core/Debugging/ComboPosition.cs` | Position DTO |
| **Core (Impl)** | `Core/Debugging/CheckpointTracker.cs` | Stack-based tracker |
| **Core (Impl)** | `Core/Debugging/ComboPositionCalculator.cs` | Position calculator |
| **Core (Impl)** | `Core/Debugging/NullComboMapProvider.cs` | Default no-op provider |
| **Core (Config)** | `Core/Configuration/ComboBreakDebuggerOptions.cs` | Feature flags |
| **Infra (Pipeline)** | `Infrastructure/Behaviors/CheckpointPipelineBehavior.cs` | MediatR auto-tracking |
| **Infra (Recorder)** | `Infrastructure/Debugging/ComboBreakRecorder.cs` | In-memory recorder |
| **Infra (Recorder)** | `Infrastructure/Debugging/NullComboBreakRecorder.cs` | No-op recorder |
| **Infra (Example)** | `Infrastructure/Debugging/ExampleComboMapProvider.cs` | Dev testing provider |
| **Infra (Tracing)** | `Infrastructure/Observability/BusinessActivitySource.cs` | ActivitySource scaffold |
| **API (Middleware)** | `API/Middleware/ComboBreakHeadersMiddleware.cs` | Header injection |
| **API (Exception)** | `API/Middleware/GlobalExceptionHandler.cs` | Checkpoint extraction + recording |
| **API (ProblemDetails)** | `API/ProblemDetails/ProblemDetailsFactory.cs` | Response enrichment |
| **API (Endpoint)** | `API/Endpoints/Debug/ComboBreak/GetAllCombosEndpoint.cs` | List combos |
| **API (Endpoint)** | `API/Endpoints/Debug/ComboBreak/GetComboByIdEndpoint.cs` | Get combo by ID |

---

## ⚙️ Configuration (appsettings.json)

```json
{
  "ComboBreakDebugger": {
    "EnableComboMeter": true,
    "EnableBusinessSpans": false,
    "ActivitySourceName": null,
    "MaxRecorderSize": 100
  }
}
```

| Option | Default | Description |
|--------|---------|-------------|
| `EnableComboMeter` | `false` | Enable combo position calculation |
| `EnableBusinessSpans` | `false` | Create child Activity spans for handlers |
| `ActivitySourceName` | `null` (auto) | Custom ActivitySource name override |
| `MaxRecorderSize` | `100` | Max snapshots in memory |

---

## 🚀 Phase Roadmap

| Phase | Status | Description |
|-------|--------|-------------|
| **Phase 0** | ✅ Complete | Contract Foundation (HttpConstants, ComboBreakSnapshot) |
| **Phase 1** | ✅ Complete | Local-Only MVP (Tracker, Pipeline, Headers, Logs) |
| **Phase 2** | ✅ Complete | Dev-Only Black Box (In-Memory Recorder, `/_debug/combos` endpoint) |
| **Phase 3** | ✅ Complete | Enterprise Scaffold (ActivitySource, ComboMap, ComboPosition) |

---

## 🧪 Testing & Demo

### Test Endpoint
```
GET /api/v1/debugging/combo-break-test?failAt={0-4}
```

| `failAt` | Behavior | Expected Headers |
|----------|----------|------------------|
| `0` | All steps pass | (No error headers) |
| `1` | Fails at ValidateInput | Current: `step:ValidateInput`, Last: `null` |
| `2` | Fails at FetchData | Current: `step:FetchData`, Last: `step:ValidateInput` |
| `3` | Fails at ProcessData | Current: `step:ProcessData`, Last: `step:FetchData` |
| `4` | Fails at SaveResult | Current: `step:SaveResult`, Last: `step:ProcessData` |

### Debug Endpoint
```
GET /_debug/combos
```
Returns all recorded combo breaks with full context including ComboPosition.

---

## 🔥 2026-01-19 Hotfixes Applied

| # | Bug | Fix |
|---|-----|-----|
| 1 | Stack corruption on MaxStackDepth | `Enter()` always pushes; telemetry bounded, not stack |
| 2 | Headers/ProblemDetails missing for Result.Fail | Read `ICheckpointTracker` directly, not Items |
| 3 | `Fail()` produced `:exception` when Current is null | Null-safe switch expression |
| 4 | Route path high-cardinality | Uses `RouteEndpoint.RoutePattern.RawText` |

---

**END OF BUSINESS RULES**
