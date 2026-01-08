# 🚀 Phase 3 Milestone: Architecture & Engineering Review

**Date:** 2025-12-17
**Status:** Production Ready
**Target:** Technical Lead Review

This document summarizes the engineering decisions, architectural patterns, and performance optimizations implemented to bridge the gap between a standard CRUD application and an Enterprise-Grade ERP system.

---

## 1. 🧠 Business Intelligence & Data Integrity

### 1.1 Dynamic "God View" Dashboard
*   **Implementation:** `DashboardReadDac.cs`
*   **Technical Detail:** Implemented complex, real-time SQL aggregation to calculate "Pending Vacancies" dynamically `((ActiveProjects * 3) - FilledPositions)`. This eliminates the need for background synchronization jobs or eventually consistent views.
*   **Impact:** Provides instant, ACID-compliant staffing intelligence for decision-makers.

### 1.2 The "Zombie Protocol" (Soft Deletes)
*   **Implementation:** `Architecture-Current-State.md` / PostgreSQL Partial Indexes
*   **Technical Detail:** Enforced referential integrity on "deleted" records using `WHERE is_deleted = FALSE` partial indexes. This allows strict uniqueness constraints (e.g., `UNIQUE(ClientCode)`) to apply only to active records while preserving historical data for audit.
*   **Impact:** Zero data loss guarantee while maintaining clean active operational tables.

### 1.3 Matrix-Level RBAC with Overrides
*   **Implementation:** `GetUserPermissions/Endpoint.cs`
*   **Technical Detail:** Implemented a hierarchical permission calculator: `Effective = (RolePermissions ∪ AllowedOverrides) \ DeniedOverrides`.
*   **Impact:** Allows precise, efficient exceptions (e.g., granting a specific feature to a user) without polluting the global Role schema.

---

## 2. 🛡️ System Resilience & Stability

### 2.1 "Self-Healing" Resilience Pipelines (Polly V8)
*   **Implementation:** `ResiliencePolicies.cs`
*   **Technical Detail:** Configured comprehensive resilience strategies including:
    *   **Retry with Jitter:** Prevents "Thundering Herd" scenarios during database blips.
    *   **Circuit Breakers:** Fails fast during outages to prevent resource exhaustion.
*   **Impact:** System can recover gracefully from transient cloud infrastructure failures without manual intervention.

### 2.2 Tenant-Partitioned Rate Limiting
*   **Implementation:** `Program.cs` / ASP.NET Core Rate Limiting
*   **Technical Detail:** Implemented `PartitionedRateLimiter` keyed by Tenant ID.
*   **Impact:** "Noisy Neighbor" protection. A denial-of-service script from one tenant will trigger `429 Too Many Requests` for them specifically, isolating the blast radius and protecting other tenants.

### 2.3 "Stampede-Proof" Caching & Cold-Start Warmup
*   **Implementation:** `CacheWarmupHostedService.cs`, `CachedTenantConnectionFactory.cs`
*   **Technical Detail:**
    *   **Warmup:** Leveraged `IHostedService` to pre-load tenant configurations into `IMemoryCache` before the application accepts traffic (K8s Readiness Probe pattern).
    *   **Locking:** Utilized `GetOrCreateAsync` for thread-safe access, ensuring only *one* thread fetches data during a cache miss while others wait, preventing database connection spikes.

---

## 3. ⚡ Performance & Observability

### 3.1 High-Performance Mapping (Mapperly)
*   **Implementation:** `PortalRoutingMapper.cs`
*   **Technical Detail:** Replaced runtime reflection (AutoMapper) with **Source Generators** (Mapperly). Mappings are compiled as standard C# code.
*   **Impact:** Zero startup cost for mappings and significant reduction in GC pressure for high-throughput endpoints.

### 3.2 Forensic Traceability
*   **Implementation:** `CorrelationIdMiddleware.cs` / Serilog
*   **Technical Detail:** Enforced a unique `Correlation-ID` across the entire request lifespan. This ID is injected into:
    1.  Response Headers (for client tracking).
    2.  Structured Logs (Serilog LogContext).
    3.  Audit Trails.
*   **Impact:** Enables O(1) debugging. We can trace a single request's journey across all logs instantly.

### 3.3 Load-Shedding Audit Architecture
*   **Implementation:** `AuditLogger.cs`
*   **Technical Detail:**
    *   **Async Processing:** Decoupled Audit Writes from the Request path using `System.Threading.Channels`.
    *   **Backpressure:** configured `BoundedChannel` with a drop-oldest strategy. If the database slows down, the system prioritizes User Traffic over Audit Logs.
    *   **Bulk Write:** Logs are committed in batches of 200 to minimize database roundtrips.

---

## 4. 🔐 Security & Operations

### 4.1 "Dual-Stack" Authentication Strategy
*   **Implementation:** `Program.cs` (Auth Schemes)
*   **Technical Detail:** Implemented a smart `ForwardDefaultSelector` that dynamically switches validation logic based on token characteristics:
    *   **RSA/OIDC:** For Enterprise SSO (Azure AD/Google).
    *   **HMAC:** For Local Development & Service Accounts.
*   **Impact:** Seamless developer experience without compromising production security standards.

### 4.2 Vertical Slice Architecture
*   **Implementation:** `CreateProject.cs` (and entire `Infrastructure/Commands` namespace)
*   **Technical Detail:** Command Logic, Validation (FluentValidation), and Data Access are co-located by Feature rather than separated by technical layer.
*   **Impact:** Reduced cognitive load for maintenance. Changes to a feature are localized to a single file/folder.

### 4.3 Ephemeral CI/CD Testing
*   **Implementation:** `.github/workflows/ci-fast.yml`
*   **Technical Detail:** Integration tests execute against real, ephemeral PostgreSQL Docker containers spawned per-build, rather than in-memory mocks.
*   **Impact:** Guarantees that complex SQL logic works on the actual production engine version.

### 4.4 Living Knowledge Base
*   **Implementation:** `READMEs/`
*   **Technical Detail:** Documentation (`BusinessRules`, `API Specs`) is committed as code, ensuring it evolves synchronously with the implementation.
