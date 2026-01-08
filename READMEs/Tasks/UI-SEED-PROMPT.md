You are an expert Frontend Engineer. Your task is to update our internal "Engineering Dashboard" (or Settings/Debug page) to reflect the new Enterprise Architecture capabilities we just finalized on the backend.



### 1. Context: What was built?
We moved from a CRUD app to an Enterprise Monolith.
- **Backend Stack:** .NET 8, PostgreSQL 18, Redis, Polly, Serilog.
- **Key Wins:** Stampede-Proof Caching, Load-Shedding Audit Logs, Matrix RBAC.

### 2. Task: Create/Update the "System Status" Component
Create a read-only "Architecture Summary" component (no charts, just clean typography/tables/lists) that displays the following "Live Status" information. You can mock the values for now, but design the UI to look like it's reading live metadata.

#### Section A: Intelligence & Data (The "Brain")
Display these capability flags:
- **Staffing Intelligence Engine:** `ONLINE` (Calculates vacancies in real-time)
- **Zombie Protocol:** `ACTIVE` (Soft-deleted Partial Indexes enforcing uniqueness)
- **RBAC Mode:** `MATRIX_LEVEL` (Role + Overrides active)

#### Section B: Resilience & Stability (The "Shield")
List these active protection protocols:
- **Resilience Policy:** `Polly v8` (Jitter + Circuit Breakers enabled)
- **Rate Limiting:** `PARTITIONED_BY_TENANT` (Noisy neighbor protection active)
- **Cache Strategy:** `STAMPEDE_PROOF` (Lock-based cold start warmup enabled)

#### Section C: Observability (The "Eyes")
Show these tracing configs:
- **Traceability:** `CORRELATION_ID` (Injected into every Header/Log)
- **Audit Strategy:** `ASYNC_LOAD_SHEDDING` (Bounded Channel Queue)
- **Mapper:** `COMPILE_TIME` (Mapperly Source Generators)

### 3. Design Requirements
- **Style:** Clean, "System Admin" aesthetic. Dark mode preferred.
- **Tone:** Technical and reassuring. Green status indicators.
- **Format:** Use a definition list (`<dl>`) or a simple Grid card layout.
- **No Images:** relies purely on text, badges, and structure.

### 4. Implementation Details
- **File:** `src/components/debug/SystemArchitectureStatus.tsx` (or similar)
- **Props:** None (It's a static system report for now).

*Goal: I need to open this page during a demo and say "Look, all these enterprise protocols are active."*
