# Seed Prompt — TASK-011: Feature Flag Evaluation Tests

Copy everything below this line and paste as your first message in a new Claude Code session.

---

## Hydration Order

Before doing anything, read these files in this exact order to rebuild context:

1. `READMEs/State/hot-state.md` — current project state, what's done, what's left
2. `READMEs/Tasks/TASK-011-Feature-Flag-Evaluation-Endpoints.md` — the full spec (Phases 1-3 are DONE, only tests remain)

Then read the implementation files you'll be TESTING (not modifying):

3. `Rgt.Space.Infrastructure/Services/Features/FeatureEvaluator.cs` — the pure static evaluator you'll unit test
4. `Rgt.Space.Infrastructure/Queries/Features/EvaluateFeature.cs` — single eval handler you'll unit test
5. `Rgt.Space.Infrastructure/Queries/Features/BulkEvaluateFeatures.cs` — bulk eval handler you'll unit test
6. `Rgt.Space.Core/Constants/FeatureFlagConstants.cs` — reason codes (10 total) for test assertions
7. `Rgt.Space.Core/ReadModels/FeatureDecision.cs` — the return type
8. `Rgt.Space.Core/ReadModels/FeatureReadModel.cs` — feature shape for test data
9. `Rgt.Space.Core/ReadModels/ClientFeatureReadModel.cs` — client sub shape for test data
10. `Rgt.Space.Core/ReadModels/UserOverrideReadModel.cs` — override shape for test data

Then read existing test files for PATTERNS to follow:

11. `Rgt.Space.Tests/Unit/Services/FeatureGateTests.cs` — 21 tests, shows helper methods + AAA pattern
12. `Rgt.Space.Tests/Unit/Handlers/Features/ReadEndpointHandlerTests.cs` — handler unit test pattern
13. `Rgt.Space.Tests/Integration/Persistence/FeatureDacIntegrationTests.cs` — DAC integration test pattern
14. `Rgt.Space.Tests/Integration/Api/FeatureEndpointTests.cs` — endpoint integration test pattern
15. `Rgt.Space.Tests/CLAUDE.md` — test conventions, naming, patterns

After hydration, confirm you understand what tests to write and tell me when you're ready. Do NOT start coding until I say go.

## What You're Building — TESTS ONLY

All production code is already implemented and building (0 errors, 0 warnings, 80/80 unit tests green). Your job is to write the test suite. **Do not modify any production code.**

### Test Files to Create (4 categories)

**1. FeatureEvaluatorTests.cs** — `Tests/Unit/Services/FeatureEvaluatorTests.cs`
- ~10 tests covering all 10 reason codes
- Pure function — NO mocks needed, just construct read models and call `FeatureEvaluator.Evaluate()`
- Test data: create `FeatureReadModel`, `ClientFeatureReadModel`, `UserOverrideReadModel` inline
- Key tests:
  - `Evaluate_ShouldReturnFeatureNotFound_WhenFeatureIsNull`
  - `Evaluate_ShouldReturnGlobalOff_WhenFeatureNotActive`
  - `Evaluate_ShouldReturnInvalidClientId_WhenRequiresClientAndClientIdEmpty`
  - `Evaluate_ShouldReturnClientNotSubscribed_WhenNoClientFeature`
  - `Evaluate_ShouldReturnClientOff_WhenClientFeatureDisabled`
  - `Evaluate_ShouldReturnUserForceOff_WhenOverrideForceOff`
  - `Evaluate_ShouldReturnUserForceOn_WhenOverrideForceOn`
  - `Evaluate_ShouldReturnGrantedSystem_WhenSystemFeatureAllGatesPassed`
  - `Evaluate_ShouldReturnGrantedClient_WhenClientFeatureAllGatesPassed`
  - `Evaluate_ShouldReturnClientOff_WhenClientOffEvenWithUserForceOn` (truth table row 9)

**2. EvaluateFeatureHandlerTests.cs** — `Tests/Unit/Handlers/Features/EvaluateFeatureHandlerTests.cs`
- ~2-3 tests, mock `IFeatureGate`
- Test success path + verify IFeatureGate receives correct params (clientId defaults to Guid.Empty when null)

**3. BulkEvaluateFeaturesHandlerTests.cs** — `Tests/Unit/Handlers/Features/BulkEvaluateFeaturesHandlerTests.cs`
- ~3-4 tests, mock `IFeatureReadDac`
- Test: returns all features evaluated, skips client sub fetch when clientId null, response shape correct

**4. Integration Tests** — add to existing files:
- `FeatureDacIntegrationTests.cs` — +2 tests for `GetClientFeaturesByClientAsync` + `GetUserOverridesByUserAsync`
- `FeatureEndpointTests.cs` — +2-4 tests for the 2 new endpoints (single eval + bulk eval, 400 on missing userId)

### Swarming Guidance
Can swarm with 2 agents:
- **Agent A**: FeatureEvaluatorTests.cs + handler unit tests (3 files, all unit, no Docker)
- **Agent B**: DAC integration tests + endpoint integration tests (2 existing files, Docker required)

### Key Conventions
- Naming: `[Method]_Should[Result]_When[Condition]`
- Traits: `[Trait("Category", "Unit")]` or `[Trait("Category", "Integration")]`
- AAA: `// Arrange` → `// Act` → `// Assert`
- Test data isolation: `$"PREFIX_{Guid.NewGuid():N}".ToUpper()[..20]` for unique codes
- `ON CONFLICT (id) DO NOTHING` for seeding (not bare `ON CONFLICT DO NOTHING`)
- FluentAssertions v6 (pinned)
- NSubstitute for mocks, `Arg.Any<CancellationToken>()` for ct params

### Current State
- Branch: `test/rbac-positiontype-verification-8034414594985432536`
- Build: 0 errors, 0 warnings
- Tests: 80 unit + 43 integration = 123 total, all green
- Target: ~139-145 total after all test files written
- Docker required for integration tests (Testcontainers)
