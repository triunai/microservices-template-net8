# TASK-007: Combo-Break Debugger — Repository Layer Auto-Tracking

**Status:** 🚧 Draft  
**Type:** Enhancement  
**Owner:** @Khumeren  
**Depends On:** TASK-006 (Combo-Break Debugger - Complete)  
**Estimated Effort:** 2-3 hours

---

## 🧠 Context & Mental Model

*We're extending the Combo-Break Debugger to auto-track at the DAC/Repository layer, so failure location is immediately obvious without manual `InStep()` calls.*

> "When an error occurs, developers should instantly know: did it fail in the **handler logic** or in the **database layer**? Right now, Level 1 only tells you 'handler:CreateClientCommand failed.' After this enhancement, you'll see 'repo:CreateAsync failed' — no extra effort required."

**Mental Model:** The checkpoint stack should show the full call path:
```
handler:CreateClientCommand → repo:CreateAsync → 💥
```

---

## ⛔ Non-Goals (Scope Fence)

- ❌ We are NOT adding tracking to individual SQL queries (too granular).
- ❌ We are NOT modifying the `Last` immutability contract.
- ❌ We are NOT tracking parallel `Task.WhenAll` fan-outs (stack model doesn't support it).
- ❌ We are NOT creating a base DAC class (your DACs are standalone).

---

## 📐 Architecture & Contracts

### 1. Checkpoint Naming (The "Law")

Per existing convention in TASK-006:

| Layer | Format | Example |
|-------|--------|---------|
| Repository/DAC | `repo:{ClassName}:{MethodName}` | `repo:ClientWriteDac:CreateAsync` |

**Low-Cardinality Rule:** Use `nameof()` for method names. Never include IDs, GUIDs, or SQL.

### 2. Stack Behavior

When DAC method is called:
```
Enter: handler:CreateClientCommand
  Enter: repo:ClientWriteDac:CreateAsync
    💥 Exception
  Fail: Current = repo:ClientWriteDac:CreateAsync
Exit: handler fails, doesn't Complete
```

**Result:**
```json
{
  "handler": "CreateClientCommand",
  "checkpointCurrent": "repo:ClientWriteDac:CreateAsync",
  "checkpointLast": "step:ValidateInput"  // or null if no prior steps
}
```

### 3. Parallel Execution Guardrail

If DAC methods are called in `Task.WhenAll`, they should **skip** tracking to avoid stack corruption:

```csharp
// ⚠️ Parallel calls — tracking skipped automatically
await Task.WhenAll(
    _clientDac.GetByIdAsync(clientId),
    _projectDac.GetByIdAsync(projectId)
);
```

**Detection:** Check if `_tracker.Current` starts with `repo:` (meaning we're already inside a repo checkpoint — likely overlap/fan-out).

---

## 📝 Implementation Plan

### Phase 1: Infrastructure (The Foundation)

**Goal:** Create a wrapper utility that all DACs can use.

- [ ] **Read Files:**
  - `Rgt.Space.Core/Abstractions/Debugging/ICheckpointTracker.cs`
  - `Rgt.Space.Infrastructure/Persistence/Dac/PortalRouting/ClientWriteDac.cs` (representative pattern)

- [ ] **Create:** `Rgt.Space.Infrastructure/Persistence/TrackedDacExecutor.cs`
  ```csharp
  public sealed class TrackedDacExecutor
  {
      private readonly ICheckpointTracker _tracker;
      
      public async Task<T> ExecuteAsync<T>(
          string dacName, 
          string methodName, 
          Func<Task<T>> work);
      
      public async Task ExecuteAsync(
          string dacName, 
          string methodName, 
          Func<Task> work);
  }
  ```

- [ ] **Register:** Add to `Extensions.cs` as `Scoped` (same lifetime as tracker)

### Phase 2: DAC Integration (The Rollout)

**Goal:** Integrate tracking into high-value DACs first.

- [ ] **Priority 1 — Write DACs (highest failure risk):**
  - `ClientWriteDac`
  - `ProjectWriteDac`
  - `UserWriteDac`
  - `RoleWriteDac`

- [ ] **Priority 2 — Read DACs (lower risk, but still useful):**
  - `ClientReadDac`
  - `ProjectReadDac`
  - `UserReadDac`

**Pattern for each DAC:**
```csharp
public sealed class ClientWriteDac : IClientWriteDac
{
    private readonly TrackedDacExecutor _executor;  // ← Add

    public async Task CreateAsync(Guid id, string name, ...)
    {
        var connString = await _systemConnFactory.GetConnectionStringAsync(ct);
        await _executor.ExecuteAsync(
            nameof(ClientWriteDac), 
            nameof(CreateAsync), 
            async () =>
            {
                await _pipeline.ExecuteAsync(async token =>
                {
                    // Existing Dapper code
                }, ct);
            });
    }
}
```

### Phase 3: ComboMap Enhancement (Optional)

**Goal:** Declare combos that include repo steps for flagship flows.

- [ ] **Update:** `ExampleComboMapProvider` with repo-aware combos:
  ```csharp
  // Use canonical checkpoint tokens (with prefixes) for exact matching
  ["CreateClientCommand"] = new[] { 
      "step:ValidateInput", 
      "step:CheckDuplicates", 
      "repo:ClientWriteDac:CreateAsync"
  }
  ```

- [ ] **Verify:** `/_debug/combos` shows ComboPosition with repo steps

### Phase 4: Verification (The Proof)

- [ ] **Manual Test — Happy Path:**
  1. Create a client via API
  2. Verify no checkpoint headers (success case)

- [ ] **Manual Test — DAC Failure:**
  1. Simulate DB connection failure (stop Postgres or use invalid connection)
  2. Hit create endpoint
  3. Verify headers show: `X-Checkpoint-Current: repo:ClientWriteDac:CreateAsync`

- [ ] **Manual Test — Debug Endpoint:**
  1. After failure, hit `GET /_debug/combos`
  2. Verify snapshot shows correct `checkpointCurrent` at repo layer

- [ ] **Guardrail Test — Parallel Calls:**
  1. Find or create a handler with `Task.WhenAll` DAC calls
  2. Verify no stack corruption (or tracking is skipped gracefully)

---

## 🛡️ Risk Mitigation

| Risk | Mitigation |
|------|------------|
| **Parallel InStep corruption** | Skip tracking if `_tracker.Current` **starts with** `repo:` (already inside a repo step) |
| **Performance overhead** | Tracker operations are O(1), negligible vs DB round-trip |
| **DI explosion** | Single `TrackedDacExecutor` reused across all DACs |
| **Breaking existing code** | Executor wraps existing code, no logic changes |

---

## 📁 File Changes

| File | Change |
|------|--------|
| `Infrastructure/Persistence/TrackedDacExecutor.cs` | **NEW** |
| `Infrastructure/Extensions.cs` | Add DI registration |
| `Infrastructure/Persistence/Dac/**/*.cs` | Inject executor, wrap methods |

**Estimated Files Modified:** 15-20 DACs

---

## 🎯 Success Criteria

When a DAC operation fails:
1. ✅ `X-Checkpoint-Current` shows `repo:{DacName}:{Method}`
2. ✅ `X-Checkpoint-Last` shows the last completed business/repo step, **or null** (handler entry is not a completed step)
3. ✅ `/_debug/combos` includes the failure with repo-level precision
4. ✅ No stack corruption on parallel DAC calls
5. ✅ No breaking changes to existing DAC behavior

---

## 📎 References

- [TASK-006: Combo-Break Debugger](./TASK-006-Combo-Break-Debugger.md)
- [COMBO-BREAK-DEBUGGER-RULES.md](../BusinessRules/COMBO-BREAK-DEBUGGER-RULES.md)

---

**Next Step:** Implement `TrackedDacExecutor` and integrate with `ClientWriteDac` as the pilot.
