# 🎮 Task: Implement Combo-Break Debugger

## 📋 Overview
Implement a low-friction debugging system that treats requests like fighting-game combos. Each meaningful business step = a checkpoint. If the combo breaks (exception or failed result), we instantly know the last successful checkpoint, the current checkpoint where it broke, and the correlationId/traceId.

**Mental Model:** Debugging should move from A→Z guessing to "start at the break point."

**Primary UI:** A single error summary (logs + headers + ProblemDetails) that already tells devs where it broke.

## 🏗️ Architecture
- **Module**: Cross-Cutting Observability
- **Pattern**: MediatR Pipeline Behavior + Middleware Enrichment
- **Integration Points**: CorrelationIdMiddleware, GlobalExceptionHandler, ProblemDetailsFactory, AuditLoggingBehavior

## 📜 Contract Definition (Field Taxonomy)

| Field | Type | Source | Description |
|-------|------|--------|-------------|
| `correlation_id` | `string` | `HttpContext.Items["CorrelationId"]` | Request tracking GUID |
| `checkpoint.current` | `string?` | `CheckpointTracker.Current` | The checkpoint being attempted ("frame we are in") |
| `checkpoint.last` | `string?` | `CheckpointTracker.Last` | Last completed checkpoint ("hit that connected") |
| `trace_id` | `string?` | `Activity.Current?.TraceId.ToString()` | OpenTelemetry W3C trace ID (if Activity exists) |
| `span_id` | `string?` | `Activity.Current?.SpanId.ToString()` | Current span ID (if Activity exists) |
| `route` | `string` | `httpContext.Request.Path` | HTTP route |
| `handler` | `string` | `typeof(TRequest).Name` | MediatR handler name |

**Semantic Contract (IMMUTABLE RULES):**
- `checkpoint.current` = the milestone being attempted at failure time (the break point)
- `checkpoint.last` = last successfully completed milestone (**NEVER mutated on failure**)
- On failure: the "combo break location" = `checkpoint.current` (deterministic, not inferred)
- **NO coalescing**: If `Current` is null, that is valuable information (failure before handler entry)
- **Handler identity ≠ checkpoint state**: Store raw handler name separately from checkpoint strings

**⚠️ What Phase 1-2 Does NOT Provide (Honest Limitations):**
- ❌ "Failed at step 2 of 7" → Requires declared combo list (Phase 3)
- ❌ "5 more steps expected" → Requires declared combo list (Phase 3)
- ❌ "Next step would be X" → Requires declared combo list (Phase 3)

**✅ What Phase 1-2 DOES Provide:**
- ✅ Last successful checkpoint (empirically confirmed)
- ✅ Current break point (exact location)
- ✅ "How far did we get?" (via optional history)
- ✅ Deterministic debugging without guessing

**Phase 3 Unlock (Optional ComboMap):**
For critical flows, you can declare a combo list to get the full "combo meter":
```csharp
ComboMap["CreateBookingCommand"] = ["ValidateInput", "LoadEntity", "CheckPermission", "SaveToDb", "PublishEvent"];
```
Then you get: `stepIndex`, `totalSteps`, `remaining`, `nextStep`.

## ⚠️ Constraints & Non-Goals (Enterprise Guardrails)

### Hard Rules
- ❌ NO storing raw `Activity` objects in memory
- ❌ NO AOP/reflection-heavy wrappers
- ❌ NO requiring devs to manually annotate methods
- ❌ NO auto-launching GUIs from the server
- ❌ NO PII in tags/baggage (tokens, request bodies, SQL parameters)
- ❌ NO using Activity.Baggage for correlationId (use header + tag only)
- ❌ NO coalescing `Current ?? Last` — preserve null signals
- ❌ NO mutating `_last` in `Fail()` — last success is immutable
- ❌ NO using checkpoint strings as handler identity — store separately
- ❌ NO parallel `InStep()` calls — stack model requires sequential execution (see below)

### ⚠️ Parallel Steps Constraint (IMPORTANT)

**The stack model does NOT support parallel step tracking.**

If you do:
```csharp
await Task.WhenAll(
    _tracker.InStep("step:A", ...),
    _tracker.InStep("step:B", ...)
);
```

There is no "one true Current" — two steps are active at once. A lock prevents corruption, but semantics become "last writer wins."

**Phase 1-2 Rule:** `InStep()` must not be used in parallel. Parallel fan-out must be tracked differently or not at all.

**Future Option (Phase 3+):** Add a `ConcurrentBag<string>` for parallel steps and expose `IReadOnlyList<string> CurrentParallel`.

### PII Guardrails
- `UserId` should be the safe identifier (GUID) or omitted
- Never store: JWT tokens, passwords, request bodies, SQL parameters
- Checkpoint names are **handler names only** (code references, not data)
- **Checkpoint names must be LOW-CARDINALITY** — no user data, no GUIDs, no timestamps in names

### IHttpContextAccessor Usage Caution
> We use `IHttpContextAccessor` inside pipeline behavior only; **do not capture `HttpContext` outside request flow**.
> 
> `IHttpContextAccessor` relies on `AsyncLocal<T>` and should be used carefully. `HttpContext` is not thread-safe and must not be accessed from parallel threads.
> — [Microsoft Learn](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/http-context)

### W3C Trace Context Interoperability
- Our dev headers (`X-Trace-Id`, `X-Checkpoint-*`) are for **local debugging convenience**
- Distributed tracing propagation uses **W3C `traceparent`/`tracestate` headers** (handled by OpenTelemetry in Phase 3)
- `Activity.TraceId` maps to the W3C `trace-id` field; `Activity.SpanId` maps to `parent-id`

### Failure Recording Gate (Phase 2)
- **Record:** Exceptions, System Errors, Critical Errors
- **Skip:** Validation failures, expected business rule failures (e.g., "entity not found")
- Logic: If `FluentResults.IError` is `ValidationError` or routine `BusinessRuleError` → skip recorder


## 🧠 Context Hydration (Pre-Dev Checklist)
Before implementation, verify these files:

1. **HttpConstants**: `Rgt.Space.Core/Constants/HttpConstants.cs` (add new headers/keys)
2. **CorrelationIdMiddleware**: `Rgt.Space.API/Middleware/CorrelationIdMiddleware.cs` (reference pattern)
3. **GlobalExceptionHandler**: `Rgt.Space.API/Middleware/GlobalExceptionHandler.cs` (enhance)
4. **ProblemDetailsFactory**: `Rgt.Space.API/ProblemDetails/ProblemDetailsFactory.cs` (enhance)
5. **AuditLoggingBehavior**: `Rgt.Space.Infrastructure/Behaviors/AuditLoggingBehavior.cs` (verify behavior order)
6. **Extensions.cs**: `Rgt.Space.Infrastructure/Extensions.cs` (DI registration)

---

## 📝 Implementation Plan

---

### Phase 0: Contract Foundation

#### 0.1 HttpConstants Extensions
- **File**: `Rgt.Space.Core/Constants/HttpConstants.cs`
- [x] Add to `Headers` class:
  ```csharp
  public const string CheckpointLast = "X-Checkpoint-Last";
  public const string CheckpointCurrent = "X-Checkpoint-Current";
  ```
- [x] Add to `ContextKeys` class:
  ```csharp
  public const string CheckpointLast = "CheckpointLast";
  public const string CheckpointCurrent = "CheckpointCurrent";
  ```

#### 0.2 ComboBreakSnapshot DTO
- **File**: `Rgt.Space.Core/Debugging/ComboBreakSnapshot.cs`
- [x] Create record:
  ```csharp
  public sealed record ComboBreakSnapshot
  {
      public required string CorrelationId { get; init; }
      public required DateTimeOffset Timestamp { get; init; }
      public string? CheckpointCurrent { get; init; }
      public string? CheckpointLast { get; init; }
      public string? TraceId { get; init; }
      public string? SpanId { get; init; }
      public required string Route { get; init; }
      public required string Handler { get; init; }
      public required string ErrorCode { get; init; }
      public required string ErrorMessage { get; init; }
      public string? TenantId { get; init; }
      public Guid? UserId { get; init; }  // Safe GUID, no PII
  }
  ```

---

### Phase 1: Local-Only MVP

#### 1.1 ICheckpointTracker Interface
- **File**: `Rgt.Space.Core/Abstractions/Debugging/ICheckpointTracker.cs`
- [x] Create interface:
  ```csharp
  public interface ICheckpointTracker
  {
      /// <summary>The checkpoint currently being attempted (top of stack).</summary>
      string? Current { get; }
      
      /// <summary>The last successfully completed checkpoint.</summary>
      string? Last { get; }
      
      /// <summary>Full history of completed checkpoints (for debugging context).</summary>
      IReadOnlyList<string> History { get; }
      
      /// <summary>When the tracker was last updated.</summary>
      DateTimeOffset? LastUpdatedAt { get; }
      
      /// <summary>Mark entry to a checkpoint (pushes to stack).</summary>
      void Enter(string checkpoint);
      
      /// <summary>Mark successful completion (pops stack, adds to history, sets Last).</summary>
      void Complete();
      
      /// <summary>Mark failure (keeps Current as the break point, does NOT mutate Last).</summary>
      void Fail(string? suffix = null);
      
      /// <summary>
      /// Execute work within a checkpoint scope (async). Auto-handles Enter/Complete/Fail.
      /// This is the SAFE way to wrap nested steps.
      /// </summary>
      Task<T> InStep<T>(string checkpoint, Func<Task<T>> work);
      
      /// <summary>
      /// Execute work within a checkpoint scope (sync). Auto-handles Enter/Complete/Fail.
      /// </summary>
      T InStep<T>(string checkpoint, Func<T> work);
  }
  ```

#### 1.2 CheckpointTracker Implementation (Stack-Based with Nesting Support)
- **File**: `Rgt.Space.Core/Debugging/CheckpointTracker.cs`
- [x] Implement with **stack for nested steps** and **Activity tags (not events)**:
  ```csharp
  public sealed class CheckpointTracker : ICheckpointTracker
  {
      private readonly Stack<string> _stack = new();  // ⚠️ Stack<T> has no capacity ctor
      private readonly List<string> _history = new(capacity: 16); // Completed steps
      private string? _last;
      private DateTimeOffset? _lastUpdatedAt;
      private const int MaxHistorySize = 20;
      private const int MaxStackDepth = 8;  // Bounded nesting (enforced manually)
      
      public string? Current => _stack.Count > 0 ? _stack.Peek() : null;
      public string? Last => _last;
      public IReadOnlyList<string> History => _history;
      public DateTimeOffset? LastUpdatedAt => _lastUpdatedAt;
      
      public void Enter(string checkpoint)
      {
          // Enforce bounded nesting
          if (_stack.Count >= MaxStackDepth)
          {
              // Log warning but don't crash - just skip tracking deeper nesting
              return;
          }
          
          _stack.Push(checkpoint);
          _lastUpdatedAt = DateTimeOffset.UtcNow;
          
          // ⚠️ Use Activity TAGS (not Events) to avoid high cardinality
          Activity.Current?.SetTag("checkpoint.current", checkpoint);
      }
      
      public void Complete()
      {
          if (_stack.Count == 0) return;
          
          var completed = _stack.Pop();
          _last = completed;
          _lastUpdatedAt = DateTimeOffset.UtcNow;
          
          // Add to history (bounded)
          _history.Add(completed);
          if (_history.Count > MaxHistorySize)
          {
              _history.RemoveAt(0);
          }
          
          // Update Activity tags
          Activity.Current?.SetTag("checkpoint.last", _last);
          Activity.Current?.SetTag("checkpoint.current", Current);  // May be null or parent
      }
      
      public void Fail(string? suffix = null)
      {
          // ⚠️ CRITICAL: DO NOT pop stack — Current IS the break point
          // ⚠️ CRITICAL: DO NOT overwrite _last — preserve last known good
          
          _lastUpdatedAt = DateTimeOffset.UtcNow;
          
          var failedAt = suffix != null 
              ? $"{Current}:{suffix}" 
              : Current;
          
          // Add failure event (events are OK for failures - low cardinality)
          Activity.Current?.AddEvent(new ActivityEvent($"failed:{failedAt}"));
          Activity.Current?.SetTag("checkpoint.broken", failedAt);
          
          // After Fail():
          //   Current = the checkpoint where failure occurred (break point)
          //   Last = last successfully completed checkpoint (immutable)
      }
      
      /// <summary>
      /// Execute async work within a checkpoint scope.
      /// ⚠️ This is the CORRECT pattern — IDisposable can't detect exceptions!
      /// Usage: var result = await _tracker.InStep("repo:SaveUser", () => _repo.SaveUser(...));
      /// </summary>
      public async Task<T> InStep<T>(string checkpoint, Func<Task<T>> work)
      {
          Enter(checkpoint);
          try
          {
              var result = await work();
              Complete();
              return result;
          }
          catch
          {
              Fail("exception");
              throw;
          }
      }
      
      /// <summary>
      /// Execute sync work within a checkpoint scope.
      /// </summary>
      public T InStep<T>(string checkpoint, Func<T> work)
      {
          Enter(checkpoint);
          try
          {
              var result = work();
              Complete();
              return result;
          }
          catch
          {
              Fail("exception");
              throw;
          }
      }
  }
  ```
- [x] **Register in Extensions.cs**:
  ```csharp
  services.AddScoped<ICheckpointTracker, CheckpointTracker>();
  ```

**Why NOT IDisposable Step()?**
- `Dispose()` is called whether the code succeeded or threw
- No way for `Dispose()` to know if an exception occurred
- Would call `Complete()` even on failure — **silent correctness bug**
- The `InStep<T>` wrapper correctly handles both paths

#### 1.3 CheckpointPipelineBehavior
- **File**: `Rgt.Space.Infrastructure/Behaviors/CheckpointPipelineBehavior.cs`
- [x] Implement:
  ```csharp
  public sealed class CheckpointPipelineBehavior<TRequest, TResponse> 
      : IPipelineBehavior<TRequest, TResponse>
      where TRequest : IRequest<TResponse>
  {
      private readonly ICheckpointTracker _tracker;
      private readonly IHttpContextAccessor _httpContextAccessor;
      
      public CheckpointPipelineBehavior(
          ICheckpointTracker tracker,
          IHttpContextAccessor httpContextAccessor)
      {
          _tracker = tracker;
          _httpContextAccessor = httpContextAccessor;
      }
      
      public async Task<TResponse> Handle(
          TRequest request,
          RequestHandlerDelegate<TResponse> next,
          CancellationToken cancellationToken)
      {
          var handlerName = typeof(TRequest).Name;
          
          // Store raw handler name for Recorder (handler identity ≠ checkpoint state)
          var httpContext = _httpContextAccessor.HttpContext;
          if (httpContext != null)
          {
              httpContext.Items["CurrentHandlerName"] = handlerName;
          }
          
          // Mark entry (this becomes "current" - the frame we're in)
          _tracker.Enter($"handler:{handlerName}");
          
          try
          {
              var response = await next();
              
              // ⚠️ FIX: Better FluentResults detection (handles Result<T> and Result)
              var isFailed = response switch
              {
                  FluentResults.Result r => r.IsFailed,
                  FluentResults.ResultBase rb => rb.IsFailed,
                  _ => false
              };
              
              if (isFailed)
              {
                  _tracker.Fail("result-failed");
                  return response;
              }
              
              // Success: pop from stack, add to history
              _tracker.Complete();
              return response;
          }
          catch
          {
              _tracker.Fail("exception");
              throw;
          }
      }
  }
  ```
- [x] **Register in Extensions.cs** (order matters):
  ```csharp
  cfg.AddOpenBehavior(typeof(CheckpointPipelineBehavior<,>));  // First
  cfg.AddOpenBehavior(typeof(AuditLoggingBehavior<,>));        // Second
  ```

#### 1.4 Enhance GlobalExceptionHandler
- **File**: `Rgt.Space.API/Middleware/GlobalExceptionHandler.cs`
- [x] In `TryHandleAsync`, after logging, store checkpoint info in HttpContext:
  ```csharp
  // Get checkpoint info
  var tracker = httpContext.RequestServices.GetService<ICheckpointTracker>();
  
  // ⚠️ CRITICAL: NO coalescing — store as-is, independently
  // If Current is null, that is valuable information:
  //   - failure occurred before handler entry
  //   - or outside a tracked combo
  //   - or in middleware
  var checkpointCurrent = tracker?.Current;  // Break point (may be null)
  var checkpointLast = tracker?.Last;        // Last success (immutable)
  
  // Store for header enrichment and ProblemDetails
  httpContext.Items[HttpConstants.ContextKeys.CheckpointCurrent] = checkpointCurrent;
  httpContext.Items[HttpConstants.ContextKeys.CheckpointLast] = checkpointLast;
  ```
- [x] Enhance `LogException` method with "COMBO BREAK" log line:
  ```csharp
  _logger.LogError(exception,
      "💥 COMBO BREAK | CorrelationId: {CorrelationId} | Current: {Current} | Last: {Last} | Route: {Route} | TraceId: {TraceId}",
      correlationId, checkpointCurrent, checkpointLast, httpContext.Request.Path, 
      Activity.Current?.TraceId.ToString());
  ```

#### 1.5 Enhance ProblemDetailsFactory
- **File**: `Rgt.Space.API/ProblemDetails/ProblemDetailsFactory.cs`
- [x] Add to `EnrichWithContext` method:
  ```csharp
  if (httpContext.Items.TryGetValue(HttpConstants.ContextKeys.CheckpointCurrent, out var current))
  {
      problemDetails.Extensions["checkpointCurrent"] = current?.ToString();
  }
  
  if (httpContext.Items.TryGetValue(HttpConstants.ContextKeys.CheckpointLast, out var last))
  {
      problemDetails.Extensions["checkpointLast"] = last?.ToString();
  }
  ```

#### 1.6 ComboBreakHeadersMiddleware
- **File**: `Rgt.Space.API/Middleware/ComboBreakHeadersMiddleware.cs`
- [x] Implement using `OnStarting` pattern with **fail-silent** try/catch:
  ```csharp
  public class ComboBreakHeadersMiddleware
  {
      private readonly RequestDelegate _next;
      private readonly IHostEnvironment _env;
      
      public ComboBreakHeadersMiddleware(RequestDelegate next, IHostEnvironment env)
      {
          _next = next;
          _env = env;
      }
      
      public async Task InvokeAsync(HttpContext context)
      {
          // Register header enrichment BEFORE response starts
          context.Response.OnStarting(() =>
          {
              // ⚠️ CRITICAL: Debug telemetry is NON-CRITICAL
              // Must NEVER interfere with production traffic
              try
              {
                  // Only in Development, only on error responses (4xx/5xx)
                  if (!_env.IsDevelopment() || context.Response.StatusCode < 400)
                      return Task.CompletedTask;
                  
                  // Skip for expected failures (validation, 404s are routine)
                  if (context.Response.StatusCode == 400 || context.Response.StatusCode == 404)
                      return Task.CompletedTask;
                  
                  AddHeaderIfPresent(context, HttpConstants.ContextKeys.CheckpointCurrent, 
                                              HttpConstants.Headers.CheckpointCurrent);
                  AddHeaderIfPresent(context, HttpConstants.ContextKeys.CheckpointLast, 
                                              HttpConstants.Headers.CheckpointLast);
                  
                  // TraceId if Activity exists
                  var traceId = Activity.Current?.TraceId.ToString();
                  if (!string.IsNullOrEmpty(traceId))
                  {
                      context.Response.Headers.TryAdd(HttpConstants.Headers.TraceId, traceId);
                  }
              }
              catch
              {
                  // Swallow — never break response for debug telemetry
              }
              
              return Task.CompletedTask;
          });
          
          await _next(context);
      }
      
      private static void AddHeaderIfPresent(HttpContext context, string itemKey, string headerName)
      {
          if (context.Items.TryGetValue(itemKey, out var value) && value != null)
          {
              context.Response.Headers.TryAdd(headerName, value.ToString());
          }
      }
  }
  ```
- [x] **Register in Program.cs** (early in pipeline, before rate limiter):
  ```csharp
  app.UseMiddleware<CorrelationIdMiddleware>();       // 1
  app.UseMiddleware<TenantResolutionMiddleware>();    // 2
  app.UseMiddleware<ComboBreakHeadersMiddleware>();   // 3 - NEW
  app.UseRateLimiter();                               // 4
  ```

---

### Phase 2: Dev-Only Black Box

#### 2.1 IComboBreakRecorder Interface
- **File**: `Rgt.Space.Core/Abstractions/Debugging/IComboBreakRecorder.cs`
- [ ] Create interface:
  ```csharp
  public interface IComboBreakRecorder
  {
      void Record(ComboBreakSnapshot snapshot);
      IReadOnlyList<ComboBreakSnapshot> GetAll();
      ComboBreakSnapshot? GetByCorrelationId(string correlationId);
      int Count { get; }
  }
  ```

#### 2.2 ComboBreakRecorder Implementation
- **File**: `Rgt.Space.Infrastructure/Debugging/ComboBreakRecorder.cs`
- [ ] Implement with bounded storage + Interlocked counter:
  ```csharp
  public sealed class ComboBreakRecorder : IComboBreakRecorder
  {
      private readonly ConcurrentQueue<ComboBreakSnapshot> _buffer = new();
      private int _count;
      private const int MaxSize = 100;
      
      public int Count => _count;
      
      public void Record(ComboBreakSnapshot snapshot)
      {
          _buffer.Enqueue(snapshot);
          var newCount = Interlocked.Increment(ref _count);
          
          // Bounded eviction using Interlocked
          while (newCount > MaxSize && _buffer.TryDequeue(out _))
          {
              Interlocked.Decrement(ref _count);
              newCount = _count;
          }
      }
      
      public IReadOnlyList<ComboBreakSnapshot> GetAll() 
          => _buffer.ToArray();
      
      public ComboBreakSnapshot? GetByCorrelationId(string correlationId)
          => _buffer.FirstOrDefault(s => 
              s.CorrelationId.Equals(correlationId, StringComparison.OrdinalIgnoreCase));
  }
  ```

#### 2.3 NullComboBreakRecorder (Production)
- **File**: `Rgt.Space.Infrastructure/Debugging/NullComboBreakRecorder.cs`
- [ ] Implement no-op:
  ```csharp
  public sealed class NullComboBreakRecorder : IComboBreakRecorder
  {
      public int Count => 0;
      public void Record(ComboBreakSnapshot snapshot) { }
      public IReadOnlyList<ComboBreakSnapshot> GetAll() => Array.Empty<ComboBreakSnapshot>();
      public ComboBreakSnapshot? GetByCorrelationId(string correlationId) => null;
  }
  ```

#### 2.4 Conditional Registration
- **File**: `Rgt.Space.Infrastructure/Extensions.cs`
- [ ] Add registration (needs IHostEnvironment):
  ```csharp
  // At bottom of AddInfrastructure or separate method
  public static IServiceCollection AddDebugging(
      this IServiceCollection services, 
      IHostEnvironment env)
  {
      if (env.IsDevelopment())
      {
          services.AddSingleton<IComboBreakRecorder, ComboBreakRecorder>();
      }
      else
      {
          services.AddSingleton<IComboBreakRecorder, NullComboBreakRecorder>();
      }
      
      return services;
  }
  ```

#### 2.5 Recording Integration in GlobalExceptionHandler
- **File**: `Rgt.Space.API/Middleware/GlobalExceptionHandler.cs`
- [ ] Add recording call (with failure gate logic):
  ```csharp
  // After creating problemDetails, record the break (if recorder available)
  var recorder = httpContext.RequestServices.GetService<IComboBreakRecorder>();
  if (recorder != null && ShouldRecord(exception, problemDetails.Status))
  {
      // ⚠️ Handler identity ≠ checkpoint state
      // Read raw handler name from context (set by CheckpointPipelineBehavior)
      var handlerName = httpContext.Items["CurrentHandlerName"]?.ToString() ?? "unknown";
      
      recorder.Record(new ComboBreakSnapshot
      {
          CorrelationId = correlationId ?? "unknown",
          Timestamp = DateTimeOffset.UtcNow,
          CheckpointCurrent = checkpointCurrent,  // May be null (valuable signal)
          CheckpointLast = checkpointLast,        // Last success (immutable)
          TraceId = Activity.Current?.TraceId.ToString(),
          SpanId = Activity.Current?.SpanId.ToString(),
          Route = httpContext.Request.Path,
          Handler = handlerName,  // Raw handler name, NOT checkpoint string
          ErrorCode = errorCode,
          ErrorMessage = exception.Message,
          TenantId = tenantId,
          UserId = ExtractUserId(httpContext)  // Safe GUID extraction
      });
  }
  
  // ⚠️ FIX: Policy-based failure gate (not just status code)
  private static bool ShouldRecord(Exception exception, int? statusCode)
  {
      // Always record 5xx (system errors)
      if (statusCode >= 500) return true;
      
      // Check exception type for "expected" business exceptions
      if (exception is NotFoundException or ValidationException)
          return false;  // Expected, don't clutter the black box
      
      // Skip routine HTTP status codes
      if (statusCode == 400 || statusCode == 404) return false;
      
      // Record unexpected errors (409 Conflict, 403 Forbidden, unknown exceptions)
      return true;
  }
  ```

#### 2.6 Dev Endpoints
- **File**: `Rgt.Space.API/Endpoints/Debug/ComboBreak/GetAllCombosEndpoint.cs`
- [ ] Implement:
  ```csharp
  public class GetAllCombosEndpoint : EndpointWithoutRequest<List<ComboBreakSnapshot>>
  {
      private readonly IComboBreakRecorder _recorder;
      private readonly IHostEnvironment _env;
      
      public GetAllCombosEndpoint(IComboBreakRecorder recorder, IHostEnvironment env)
      {
          _recorder = recorder;
          _env = env;
      }
      
      public override void Configure()
      {
          Get("/_debug/combos");
          AllowAnonymous();
          Description(d => d.Produces<List<ComboBreakSnapshot>>(200).Produces(403));
      }
      
      public override async Task HandleAsync(CancellationToken ct)
      {
          if (!_env.IsDevelopment())
          {
              await SendForbiddenAsync(ct);
              return;
          }
          
          await SendOkAsync(_recorder.GetAll().ToList(), ct);
      }
  }
  ```

- **File**: `Rgt.Space.API/Endpoints/Debug/ComboBreak/GetComboByIdEndpoint.cs`
- [ ] Implement:
  ```csharp
  public class GetComboByIdEndpoint : Endpoint<GetComboByIdRequest, ComboBreakSnapshot>
  {
      // Similar pattern with correlationId route param
      // Returns 404 if not found
  }
  ```

---

### Phase 3: Enterprise Tracing + Combo Meter (Optional)

#### 3.1 TracingExtensions
- **File**: `Rgt.Space.Infrastructure/Observability/TracingExtensions.cs`
- [ ] Create scaffold:
  ```csharp
  // Phase 3 - Uncomment when choosing OTel backend
  public static class TracingExtensions
  {
      public static readonly ActivitySource BusinessSource = 
          new("Rgt.Space.Portal.Business", "1.0.0");
      
      public static IServiceCollection AddObservability(
          this IServiceCollection services,
          IConfiguration configuration)
      {
          // TODO: Implement when backend chosen (Jaeger/Tempo/AppInsights)
          // services.AddOpenTelemetry()
          //     .WithTracing(builder => { ... });
          
          return services;
      }
  }
  ```

#### 3.2 Checkpoint Naming Convention (REQUIRED for Phase 3)

For ComboMap matching to work, checkpoints must follow a standardized format:

| Layer | Format | Example |
|-------|--------|--------|
| Handler entry | `handler:{HandlerName}` | `handler:CreateBookingCommand` |
| Business step | `step:{StepName}` | `step:ValidateInput` |
| Repository | `repo:{Operation}` | `repo:SaveOrder` |
| External call | `ext:{Service}:{Operation}` | `ext:Stripe:ChargeCard` |
| Domain service | `domain:{Service}:{Method}` | `domain:PricingEngine:Calculate` |

**Usage in handlers (optional, only where you want granular tracking):**
```csharp
public async Task<Result<Guid>> Handle(CreateBookingCommand request, CancellationToken ct)
{
    // Automatic: handler:CreateBookingCommand (from pipeline behavior)
    
    var validated = await _tracker.InStep("step:ValidateInput", async () => 
    {
        return await _validator.ValidateAsync(request, ct);
    });
    
    var entity = await _tracker.InStep("step:LoadEntity", async () => 
    {
        return await _repo.GetByIdAsync(request.EntityId, ct);
    });
    
    await _tracker.InStep("step:SaveToDb", async () => 
    {
        return await _repo.SaveAsync(entity, ct);
    });
    
    // etc.
}
```

#### 3.3 ComboMap — "Failed at step 2 of 7" Feature (OPTIONAL)
- **File**: `Rgt.Space.Core/Debugging/ComboMap.cs`
- [ ] Create optional combo definitions:
  ```csharp
  /// <summary>
  /// Optional: Declare expected steps for critical flows.
  /// This enables "failed at step 2 of 7, next was X" reporting.
  /// Only use for critical flows — most handlers don't need this.
  /// </summary>
  public static class ComboMap
  {
      private static readonly Dictionary<string, string[]> _combos = new()
      {
          // Example: declare expected steps for critical flows
          ["CreateBookingCommand"] = new[] 
          { 
              "ValidateInput", 
              "LoadEntity", 
              "CheckPermission", 
              "SaveToDb", 
              "PublishEvent" 
          },
          ["ProcessPaymentCommand"] = new[] 
          { 
              "ValidateAmount", 
              "LoadCustomer", 
              "AuthorizePayment", 
              "CapturePayment", 
              "UpdateLedger", 
              "SendReceipt" 
          },
      };
      
      public static bool TryGetCombo(string handlerName, out string[] steps)
          => _combos.TryGetValue(handlerName, out steps!);
      
      /// <summary>
      /// Extract step name from checkpoint string.
      /// "step:ValidateInput" -> "ValidateInput"
      /// "handler:Foo:step:ValidateInput" -> "ValidateInput"
      /// </summary>
      public static string? ExtractStepName(string? checkpoint)
      {
          if (string.IsNullOrEmpty(checkpoint)) return null;
          
          // Look for "step:" prefix
          var stepPrefix = "step:";
          var stepIdx = checkpoint.LastIndexOf(stepPrefix, StringComparison.OrdinalIgnoreCase);
          if (stepIdx >= 0)
          {
              var afterPrefix = checkpoint.Substring(stepIdx + stepPrefix.Length);
              // Take until next ":" or end
              var colonIdx = afterPrefix.IndexOf(':');
              return colonIdx > 0 ? afterPrefix.Substring(0, colonIdx) : afterPrefix;
          }
          
          return null;
      }
      
      public static ComboPosition? GetPosition(string handlerName, string? currentCheckpoint)
      {
          if (!TryGetCombo(handlerName, out var steps))
              return null;
          
          // Extract step name from checkpoint (e.g., "step:SaveToDb" -> "SaveToDb")
          var stepName = ExtractStepName(currentCheckpoint);
          if (stepName == null) return null;
          
          // Find position in declared combo (exact match)
          var index = Array.IndexOf(steps, stepName);
          if (index < 0) return null;
          
          return new ComboPosition
          {
              CurrentIndex = index,
              TotalSteps = steps.Length,
              Remaining = steps.Length - index - 1,
              NextStep = index + 1 < steps.Length ? steps[index + 1] : null,
              CompletedSteps = steps.Take(index).ToArray(),
              RemainingSteps = steps.Skip(index + 1).ToArray()
          };
      }
  }
  
  public sealed record ComboPosition
  {
      public required int CurrentIndex { get; init; }
      public required int TotalSteps { get; init; }
      public required int Remaining { get; init; }
      public string? NextStep { get; init; }
      public required string[] CompletedSteps { get; init; }
      public required string[] RemainingSteps { get; init; }
      
      public override string ToString() 
          => $"Step {CurrentIndex + 1}/{TotalSteps}, {Remaining} remaining, next: {NextStep ?? "(done)"}";
  }
  ```

#### 3.3 Enhanced ComboBreakSnapshot (Phase 3)
- [ ] Extend snapshot with combo position:
  ```csharp
  public sealed record ComboBreakSnapshot
  {
      // ... existing fields ...
      
      // Phase 3: Combo Meter (only populated if handler has ComboMap entry)
      public ComboPosition? ComboPosition { get; init; }
  }
  ```

#### 3.4 Recorder Integration (Phase 3)
- [ ] In GlobalExceptionHandler, add combo position:
  ```csharp
  // Try to get combo position (only for handlers with declared combos)
  var comboPosition = ComboMap.GetPosition(handlerName, checkpointCurrent);
  
  recorder.Record(new ComboBreakSnapshot
  {
      // ... existing fields ...
      ComboPosition = comboPosition  // May be null for most handlers
  });
  ```

**Phase 3 Output Example (Handler with ComboMap):**
```json
{
  "correlationId": "abc-123",
  "handler": "CreateBookingCommand",
  "checkpointCurrent": "handler:CreateBookingCommand:SaveToDb",
  "checkpointLast": "handler:CreateBookingCommand:CheckPermission",
  "comboPosition": {
    "currentIndex": 3,
    "totalSteps": 5,
    "remaining": 1,
    "nextStep": "PublishEvent",
    "completedSteps": ["ValidateInput", "LoadEntity", "CheckPermission"],
    "remainingSteps": ["PublishEvent"]
  }
}
```

**Key Principle:**
> "We can show 'remaining steps' only when the combo is declared. Otherwise we stay honest and only report what actually happened."

---

## ✅ Verification Checklist

### Phase 0
- [ ] `HttpConstants.cs` has new headers and context keys
- [ ] `ComboBreakSnapshot` compiles and is in correct namespace
- [ ] Contract uses `Current/Last` (not `Expected/Previous`)

### Phase 1
- [ ] Solution builds without errors
- [ ] `ICheckpointTracker` registered as Scoped
- [ ] `CheckpointTracker` uses **stack** for nested step support
- [ ] `CheckpointTracker` uses **Activity tags** (not events) for low cardinality
- [ ] `CheckpointPipelineBehavior` runs before `AuditLoggingBehavior`
- [ ] `CheckpointPipelineBehavior` uses pattern matching for FluentResults
- [ ] Error response includes `checkpointCurrent` and `checkpointLast` in extensions
- [ ] Logs show "💥 COMBO BREAK" with checkpoint info
- [ ] Dev headers only appear in Development environment
- [ ] Dev headers only appear for 5xx errors (not 400/404)

### Phase 2
- [ ] `ComboBreakRecorder` uses Interlocked (no Count in loop)
- [ ] `NullComboBreakRecorder` used in non-Development
- [ ] `/_debug/combos` returns 403 in Production
- [ ] Recorder uses **policy-based** failure gate (not just status code)
- [ ] Recorder skips expected exceptions (NotFoundException, ValidationException)

### Phase 3 (Optional)
- [ ] `ComboMap` provides step lists for critical handlers only
- [ ] `ComboPosition` computed only when handler has declared combo
- [ ] "Step 2/7, 5 remaining" only shown for declared flows

### Dev UX Test (Acceptance Criteria)
When a dev hits an error locally:
- [ ] Response headers show: `X-Checkpoint-Last`, `X-Checkpoint-Current`, `X-Correlation-Id`
- [ ] ProblemDetails body includes `checkpointLast`, `checkpointCurrent`
- [ ] One log line makes the "break point" obvious in under 10 seconds
- [ ] `/_debug/combos` shows recent failures with full context
- [ ] **Nested steps** are preserved (inner step shows as Current, handler as history)

---

## 🔧 10 Critical Production Fixes (Applied Above)

| # | Fix | Problem | Solution |
|---|-----|---------|----------|
| 1 | **Stack-Based Nesting** | Single `_current` loses context when inner steps run | Stack of checkpoints, preserves handler + inner steps |
| 2 | **Activity Tags (not Events)** | `AddEvent()` is high-cardinality, expensive | Use `SetTag()` for state, events only on failure |
| 3 | **FluentResults Detection** | `is ResultBase` doesn't match `Result<T>` | Pattern matching: `Result r => r.IsFailed` |
| 4 | **Policy-Based Failure Gate** | Status code alone is misleading | Check exception type + status code |
| 5 | **InStep<T> Wrapper (NOT IDisposable)** | `Dispose()` can't detect exceptions → calls `Complete()` on failure 💀 | Async wrapper with try/catch handles both paths |
| 6 | **Stack Constructor Fix** | `Stack<T>(capacity)` doesn't exist in .NET | Use `new Stack<T>()` + manual depth enforcement |
| 7 | **Checkpoint Naming Convention** | Inconsistent formats break ComboMap matching | Standardized: `step:`, `repo:`, `ext:`, `handler:` |
| 8 | **ComboMap Matching Logic** | `EndsWith()` is fragile, matches wrong things | `ExtractStepName()` + exact `Array.IndexOf()` |
| 9 | **Parallel Steps Constraint** | Stack model can't represent parallel work; lock only prevents crash | Explicitly forbid parallel `InStep()` in Phase 1-2 |
| 10 | **IHttpContextAccessor Caution** | `HttpContext` is not thread-safe; `AsyncLocal` has subtle rules | Use only in pipeline behavior; never capture outside request flow |

---

## 📁 File Structure (New Files)

```
Rgt.Space.Core/
├── Abstractions/
│   └── Debugging/
│       ├── ICheckpointTracker.cs          ← NEW
│       └── IComboBreakRecorder.cs         ← NEW (Phase 2)
├── Debugging/
│   ├── CheckpointTracker.cs               ← NEW
│   └── ComboBreakSnapshot.cs              ← NEW
└── Constants/
    └── HttpConstants.cs                   ← MODIFIED

Rgt.Space.Infrastructure/
├── Behaviors/
│   ├── AuditLoggingBehavior.cs            ← EXISTING
│   └── CheckpointPipelineBehavior.cs      ← NEW
├── Debugging/
│   ├── ComboBreakRecorder.cs              ← NEW (Phase 2)
│   └── NullComboBreakRecorder.cs          ← NEW (Phase 2)
├── Observability/
│   └── TracingExtensions.cs               ← NEW (Phase 3 scaffold)
└── Extensions.cs                          ← MODIFIED

Rgt.Space.API/
├── Middleware/
│   ├── GlobalExceptionHandler.cs          ← MODIFIED
│   └── ComboBreakHeadersMiddleware.cs     ← NEW
├── ProblemDetails/
│   └── ProblemDetailsFactory.cs           ← MODIFIED
├── Endpoints/
│   └── Debug/
│       └── ComboBreak/
│           ├── GetAllCombosEndpoint.cs    ← NEW (Phase 2)
│           └── GetComboByIdEndpoint.cs    ← NEW (Phase 2)
└── Program.cs                             ← MODIFIED
```

---

## 🎯 Success Criteria Summary

| Criterion | Implementation |
|-----------|---------------|
| Junior dev hits error locally | Response body + headers show checkpoint info |
| Looks at logs | `💥 COMBO BREAK` log line with Current/Last |
| Immediately knows where combo broke | `checkpoint.current` = exact handler that failed |
| Headers tell the story | `X-Checkpoint-Last`, `X-Checkpoint-Current` |
| Can dive deeper via traceId | `X-Trace-Id` links to OTel (Phase 3) |
| No heavy AOP/reflection | MediatR pipeline behavior only |
| No manual method annotations | Handler names auto-extracted |
| No Activity objects in memory | Only `ComboBreakSnapshot` stored |
| Dev-only safeguards | `IsDevelopment()` gates everywhere |
| No PII leakage | UserId is GUID only, no request bodies |
