# MultiShell Contribution & Development Workflow Guide

This document outlines the development workflow, branching strategy, Pull Request (PR) standards, subagent collaboration model, and release management policies for the **MultiShell** repository.

---

## 1. Branching Strategy (Trunk-Based / Feature Branching)

MultiShell follows a lightweight **Trunk-Based / GitHub Flow** branching model with `main` as the sole stable production branch.

### 1.1 Branch Naming Conventions

All changes must be developed in dedicated feature, fix, or maintenance branches branched off the latest `main`:

| Prefix | Category | Example | Purpose |
| :--- | :--- | :--- | :--- |
| `feat/` | New Feature | `feat/REQ-025-tab-split-view` | New feature specified in `REQUIREMENTS.md`. |
| `fix/` | Bug Fix | `fix/BUG-014-conpty-leak` | Bug fix, crash remediation, or exception fix. |
| `refactor/` | Refactoring | `refactor/clean-terminal-session` | Code cleanup with no functional behavior changes. |
| `perf/` | Performance | `perf/zero-alloc-conpty-buffer` | Memory allocation or throughput optimization. |
| `docs/` | Documentation | `docs/update-architecture-guide` | XML doc comments, markdown guides, architecture specs. |
| `chore/` | Tooling & CI | `chore/dotnet-10-package-bump` | Build script updates, GitHub Actions, dependencies. |

---

## 2. End-to-End Development Lifecycle

Every code change moves through a structured, quality-gated 8-stage lifecycle:

```mermaid
flowchart TD
    A["1. Requirement / Issue"] -->|"RequirementEngineer"| B["2. Create Branch"]
    B -->|"Architect & Developer"| C["3. Implement Clean Code & MVVM"]
    C -->|"Tester"| D["4. Automated Tests (MultiShell.Tests)"]
    D -->|"Verifikation"| E["5. Quality Gate Audit"]
    E -->|"Pass"| F["6. Developer Review & Live Testing Gate"]
    F -->|"Needs Changes / Corrections"| C
    F -->|"Approved"| G["7. Commit & Open PR via CommitManager"]
    G -->|"GitHub Actions CI"| H{"CI Build & Test Pass?"}
    H -->|"No"| C
    H -->|"Yes"| I["8. Maintainer Review & Squash Merge"]
    I -->|"ReleaseManager"| J["SemVer Tag vX.Y.Z & GitHub Release"]
```

### Stage 1: Requirement Specification (`RequirementEngineer`)
- Verify that every user story is documented in [REQUIREMENTS.md](REQUIREMENTS.md) with explicit **Given-When-Then** acceptance criteria.
- 100% of functional changes must map to approved requirement IDs (e.g., `REQ-HIST-002`).

### Stage 2: Branch Creation
```powershell
git checkout main
git pull origin main
git checkout -b feat/REQ-025-tab-split-view
```

### Stage 3: Clean Architecture Implementation (`Developer` & `UIDesigner`)
- Implement domain logic, services, view models, and views respecting layer separation:
  - **Models**: Pure domain objects, DTOs, and serialization models.
  - **Services**: Interfaces (`I...Service`) and infrastructure implementations (ConPTY Win32 bindings, theme manager, fuzzy search).
  - **ViewModels**: CommunityToolkit.Mvvm presentation logic decoupled from Avalonia UI controls.
  - **Views**: Compiled bindings (`x:DataType`), decoupled styles, zero business logic in code-behind.
- **i18n Compliance**: Zero hardcoded UI strings; all user-facing texts must be referenced in dynamic localization dictionaries (German `de` & English `en` mandatory).

### Stage 4: Automated Testing (`Tester`)
- Add or update comprehensive xUnit unit tests in [MultiShell.Tests](MultiShell.Tests/).
- Ensure the Arrange-Act-Assert (AAA) pattern is strictly followed.
- Run tests locally:
  ```powershell
  dotnet test MultiShell.Tests/MultiShell.Tests.csproj
  ```

### Stage 5: Local Quality Gate Audit (`Verifikation`)
- Verify 0 test failures, 0 compiler warnings, and clean formatting.
- Confirm full requirement coverage and bilingual localization resources.

### Stage 6: Developer Review & Live Testing Gate (Manual Verification & Pre-PR Iteration)
- Prior to staging commits and creating a Pull Request, the working state is presented to the developer/user for interactive review:
  - **Manual/Interactive Testing**: The developer can launch and run the application locally to verify real-world ergonomics, keyboard workflows, and terminal behavior.
  - **Code Review & Feedback**: The developer inspects code diffs and can request modifications, design adjustments, or edge-case handling.
  - **Pre-PR Corrections**: If issues are found, the subagents (`Developer`, `UIDesigner`, `Architekt`, `Tester`) immediately iterate and apply corrections before any PR is created or merged.
  - **Explicit Approval**: Once the developer confirms that the feature functions as expected, the workflow proceeds to Stage 7.

### Stage 7: Commit & Pull Request (`CommitManager`)
- Stage and commit changes using the **Conventional Commits** specification:
  - Format: `<type>(<scope>): <summary>`
  - Example: `feat(terminal): add tab split view and layout persistence`
- Push branch and create a Pull Request against `main`:
  ```powershell
  git push -u origin feat/REQ-025-tab-split-view
  gh pr create --fill
  ```

### Stage 8: CI Check, Review & Squash Merge (Maintainer)
- GitHub Actions CI ([.github/workflows/ci.yml](.github/workflows/ci.yml)) automatically runs builds and tests on Windows.
- The project maintainer reviews the PR and executes a **Squash and Merge**.

---

## 3. Pull Request Standards & Quality Gates

Every Pull Request must fill out [.github/pull_request_template.md](.github/pull_request_template.md):

* **Traceability**: Must reference the linked issue or requirement (`REQ-XXX`).
* **Clean History**: PRs are merged via **Squash and Merge** to maintain a linear Git history on `main`.
* **Branch Cleanup**: Remote feature branches should be automatically or manually deleted post-merge.

---

## 4. Release & Versioning Policy (`ReleaseManager`)

MultiShell adheres to **Semantic Versioning 2.0.0** (`MAJOR.MINOR.PATCH`):
- **MAJOR**: Incompatible API or structural breaking changes.
- **MINOR**: Backward-compatible new features (`feat`).
- **PATCH**: Backward-compatible bug fixes (`fix`, `perf`).

When a release milestone is reached on `main`:
1. `ReleaseManager` verifies all merged PRs and proposes the SemVer version bump.
2. Upon user confirmation, a Git tag (e.g. `v1.2.0`) is created and pushed.
3. [.github/workflows/release.yml](.github/workflows/release.yml) automatically triggers, compiles single-file self-contained executables, builds the Inno Setup installer (`installer.iss`), and generates a GitHub Release.
