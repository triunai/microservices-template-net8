# [TASK-008] Standardize DAC Resilience Patterns

**Status:** ✅ DONE

**Type:** Tech Debt

**Owner:** @You

## 🧠 Context & Mental Model

*We are standardizing resilience patterns because a code audit revealed inconsistent usage (some DACs have no resilience, others mix patterns). One sentence on "How to think about it": "Think of **Pattern A** (Injection) as the default for static, system-level databases, and **Pattern B** (MultiTenantResilience) strictly for dynamic contexts where we don't know the connection until runtime."*

> "All database operations must have retry/circuit-breaker protection. Use 'Standard Injection' for everything known at startup, and only use 'MultiTenantResilience' when the tenant ID dictates the connection."

## ⛔ Non-Goals (Scope Fence)

- ❌ We are NOT rewriting the SQL queries or modifying schemas.

- ❌ We are NOT changing resilience settings (retry counts, timeouts) yet.

- ❌ We are NOT adding new functional features, just hardening existing code.

## 📐 Architecture & Contracts

### 1. Standard Resilience (Pattern A)

**Mechanism:** `ResiliencePipelineProvider<string>` (Injected)
**Use Case:** Static, System, Master, Portal databases.
**Why:** Pipelines are registered *once* during startup in `Extensions.cs`.

```csharp
// 1. Register at startup (Extensions.cs)
services.AddResiliencePipeline("System", ...);

// 2. Inject and use (DAC)
public ClientWriteDac(ResiliencePipelineProvider<string> provider) 
{
    _pipeline = provider.GetPipeline("System"); // Simple lookup
}
```

### 2. MultiTenantResilience (Pattern B)

**Mechanism:** `ResiliencePipelineRegistry<string>` (Lazy Factory)
**Use Case:** Per-Tenant databases where keys are dynamic (e.g., `tenant_123`).
**Why:** We can't register 1000s of potential tenant pipelines at startup. We must create them *on the fly*.

```csharp
public SalesReadDac(ResiliencePipelineRegistry<string> registry)
{
    // Lazy creation: "Do we have a pipeline for 'tenant_123'? No? Build it now."
    if (!registry.TryGetPipeline(tenantId, out var pipeline))
    {
        registry.TryAddBuilder(tenantId, ...);
        pipeline = registry.GetPipeline(tenantId);
    }
    _pipeline = pipeline;
}
```

## 📝 Implementation Plan

### Phase 1: Infrastructure (The Foundation)

- [x]  **Read Files:** 
    ```
    Rgt.Space.Infrastructure/Extensions.cs
    ```
- [x]  **Register:** Add the `System` pipeline key in `Extensions.cs`.
    ```csharp
    services.AddResiliencePipeline("System", ...); // Maps to MasterDb settings
    ```
- [x]  **Register:** Add the `AuditDb` pipeline key in `Extensions.cs` (Standardized AuditLogger).

### Phase 2: Application Logic (The Meat)

- [x]  **Critical Fix:** Update `UserWriteDac.cs` (currently has NO resilience) to use **Standard Resilience (Pattern A)**.
- [x]  **Refactor:** Convert the following from "Fake MultiTenant" -> **Standard Resilience (Pattern A)**:
    - `UserReadDac.cs`
    - `TaskAllocationWriteDac.cs`
    - `ProjectAssignmentReadDac.cs`
    - `DashboardReadDac.cs`
    - `AuditLogger.cs` (Was incorrectly using Registry)
- [x]  **Clarify:** Add `<summary>` comments to `SalesReadDac.cs` confirming it correctly uses **MultiTenantResilience**.

### Phase 3: Verification (The Proof)

- [x]  **Audit:** Verified `ResiliencePipelineRegistry` is ONLY used in `SalesReadDac` (and `Extensions`).
- [x]  **Build:** Verified `dotnet build` and Visual Studio build pass cleanly.

## ✅ Completion Summary

All DACs now follow the strict "Injection for Static / Registry for Dynamic" rule. 
- **Pattern B** is exclusively used in `SalesReadDac` for dynamic tenant isolation.
- **Pattern A** is used everywhere else (`User`, `TaskAllocation`, `Dashboard`, `Audit`, `PortalRouting`).
- Build is green. Tests updated.
