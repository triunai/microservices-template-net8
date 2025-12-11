# 🚀 CI/CD Premium Experience Upgrade

## Context
The current CI workflow produces a "file explosion" of HTML reports that are difficult to consume. Developers have to download a ZIP artifact and hunt for an index file. Additionally, there is a risk of `FluentAssertions` upgrading to a paid license version.

## 🎯 Objectives
1.  **Imrpove CI Reporting:** Switch to a summary-first approach using "Premium" report types.
2.  **Safety Lock:** Pin `FluentAssertions` to safe V6/V7 versions to avoid licensing issues.
3.  **Job Summary:** Inject a markdown summary directly into the GitHub Actions run summary so no download is needed.

## 🛠️ Implementation Plan

### 1. Update `ci.yml`
Modify the `Generate coverage report` step to use smarter report types.

```yaml
      - name: Generate coverage report
        run: |
          reportgenerator \
            -reports:"**/coverage.cobertura.xml" \
            -targetdir:"coverage-report" \
            -reporttypes:"HtmlInline_AzurePipelines_Dark;Summary;MarkdownSummary"
```

Then, add a step to publish the **Markdown Summary** to the GitHub Job Summary page.

```yaml
      - name: Publish Coverage Summary
        run: |
          cat coverage-report/Summary.md >> $GITHUB_STEP_SUMMARY
```

### 2. Lock dependencies in `Rgt.Space.Tests.csproj`
Verify and lock `FluentAssertions`.

```xml
    <!-- Safety Lock: Prevent accidental upgrade to V8 (Commercial License) -->
    <PackageReference Include="FluentAssertions" Version="6.12.1" /> 
```
*Note: We will stick to 6.12.1 for now as it is stable and already installed.*

## 🧪 Verification
1.  Commit changes.
2.  Push to branch.
3.  Observe GitHub Actions run:
    *   Verify `Summary.md` content appears in the "Job Summary" view.
    *   Verify "Download Artifact" contains a clean single-file HTML report (or at least the Summary.html).
