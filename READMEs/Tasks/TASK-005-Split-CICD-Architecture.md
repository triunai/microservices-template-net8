# 🏗️ Split CI/CD Architecture Strategy

## Context
Refactoring the monolithic CI workflow into three specialized workflows to optimize for speed, security, and stability.

## 🎯 Architecture
### 1. `ci-fast.yml` (Velocity)
*   **Trigger:** Push to any feature branch.
*   **Role:** Rapid feedback loop for developers.
*   **Tasks:** Build, Unit Test, Integration Test.
*   **Artifacts:** Cobertura XML (for manual review if needed).

### 2. `ci-quality.yml` (Governance)
*   **Trigger:** PRs to `main` ONLY (excluding Dependabot).
*   **Role:** Enforce Quality Gates.
*   **Tasks:** SonarCloud "Sandwich" (Begin -> Build -> Test -> End).
*   **Secrets:** Requires `SONAR_TOKEN`.
*   **Gate:** Fails if "New Code" quality metrics are not met.

### 3. `dependabot-check.yml` (Maintenance)
*   **Trigger:** PRs from `dependabot[bot]`.
*   **Role:** Safe validation of external packages.
*   **Tasks:** Build & Test only.
*   **Safety:** Does NOT attempt to access secrets.

## 🛠️ Implementation Plan

### Step 1: Rename `ci.yml` -> `ci-fast.yml`
*   No functional changes, just renaming for clarity.
*   Keep the beautiful `MarkdownSummary` we just built.

### Step 2: Create `ci-quality.yml`
*   **Environment:** Needs `java-17` (Wait, per analysis: modern scanner handles JRE. We will trust the scanner).
*   **Tool:** `dotnet tool install --global dotnet-sonarscanner`
*   **Flow:**
    1.  `dotnet-sonarscanner begin` (Key: `triunai_microservices-template-net8`)
    2.  `dotnet build`
    3.  `dotnet test` (Collect: `XPlat Code Coverage`)
    4.  `dotnet-sonarscanner end` (Wait for Gate)

### Step 3: Create `dependabot-check.yml`
*   Minimal workflow.
*   Explicitly stripped of any reporting steps that might fail without tokens.
*   Just `restore` -> `build` -> `test`.

## 📝 Configuration Notes
*   **SonarCloud Project Key:** `triunai_microservices-template-net8`
*   **SonarCloud Org:** `triunai`
*   **Exclusions:** `**/Migrations/**`, `**/Program.cs` (Standard noise reduction).
