# Workspace Agent Guidelines: Subagent Roles & Context Isolation

## Overview
This workspace employs specialized subagent roles to enforce **Clean Code**, **Clean Architecture**, modern **C# 13 / .NET 10 / Avalonia UI 11.2** best practices, maximum **UI/UX usability**, full **Internationalization (i18n & l10n)**, high performance, robust security, systematic **Root Cause Analysis (Troubleshooting)**, high automated test coverage, and strict context isolation.

All skills and governance rules reference the Single Source of Truth in `_agents/rules/model-tiers.json` and adapt dynamically to the project's technical conventions.

## Core Governance & Architecture Rules
1. **Clean Architecture Principles**:
   - Strict separation of concerns across layers (Domain/Entities -> Application/Service Contracts -> ViewModels/Adapters -> Views/Presentation).
   - Core domain and service interfaces must remain agnostic of UI frameworks and external system details.
2. **Clean Code & Modern Best Practices**:
   - SOLID, DRY, KISS, YAGNI, Boy Scout Rule, and descriptive naming.
   - Modern C# idioms (nullable reference types, async/await with cancellation tokens, records, pattern matching, efficient collection expressions).
   - Avalonia UI conventions (compiled bindings `x:DataType`, CommunityToolkit.Mvvm source generators, decoupled XAML styles).
   - All source code comments and docstrings must be written in English.
3. **Core Workflow vs. Domain & Lifecycle Specialists**:
   - **Core Lifecycle Roles** handle the general development lifecycle: `Control`, `RequirementEngineer`, `Architekt`, `Developer`, `Tester`, `Verifikation`, `CommitManager`, `PRManager`.
   - **Lifecycle & Domain Specialists** (e.g., `Troubleshooter`, `GitTroubleshooter`, `RefactoringSpecialist`, `ArchitectureSync`, `DocumentationSpecialist`, `DevOpsEngineer`, `UIDesigner`, `LocalizationSpecialist`, `PerformanceOptimizer`, `SecurityAuditor`, `DatabaseSpecialist`, `ApiContractSpecialist`, `ReleaseManager`, `Tiebreaker`, `CodeExplainer`) are engaged **on-demand**.
   - **On-Demand Specialist Invocation**: Specialists are not mandatory serial steps on every commit. They are engaged selectively when features or bugs explicitly touch their specific domain.
4. **Automated Testing, Quality & Inner-Loop TDD**:
   - Standard feature development and bugfixing adhere to **Inner-Loop Test-Driven Development (TDD)** (Red-Green-Refactor) adapted to task complexity:
     - **Profile A (Fast-Track - Bugs, Tweaks, Small Features)**: Developer authors targeted unit tests and implementation code directly in a single, fast red-green-refactor loop.
     - **Profile B (Standard Features)**: Architecture contract established -> Developer implements unit tests and code via Inner-Loop TDD -> Verifikation quality gate.
     - **Profile C (Complex / Architectural)**: Modular architecture contracts -> Optional skeleton stubs for parallel decoupling -> Comprehensive integration test suites by `Tester` -> Developer implementation.
   - **Targeted Test Execution**: During inner loops, test runners must target only the affected test file or class (`dotnet test --filter`), executing the full test suite once during final verification to prevent full-suite build thrashing.
   - **Two-Stage Quality Gate (Shift-Left Validation)**:
     - **Stage 1 (Deterministic Fast-Gate - Zero Tokens)**: Native build (`dotnet build`), project linter, and quiet native test runner (`dotnet test --verbosity quiet`, 0 errors, 100% pass). If failing, immediately return for remediation without consuming LLM tokens on semantic analysis.
     - **Stage 2 (Concise Traceability Gate)**: `Verifikation` audits acceptance criteria fulfillment and Clean Architecture boundaries.
   - **Test Integrity Guardrail**: Replaces rigid test immutability. The Developer is empowered to adjust and refine test fixtures, signatures, and assertions to match real contracts and idiomatic types. Weakening, bypassing, or deleting assertions to fake passing tests is strictly forbidden.
   - **Circuit Breaker**: The `Developer`'s targeted test-fix feedback loop is capped at a maximum of 3 iterations before escalating.
   - **Comprehensive Scenario Coverage**: Unit and integration tests follow the Arrange-Act-Assert (AAA) pattern with a required 100% pass rate (0 failures).
5. **Internationalization & Localization (i18n / l10n)**:
   - 0% hardcoded user-facing strings; all texts must be managed in dynamic bilingual resource dictionaries in **German (`de`)** and **English (`en`)** (with expanded language support for French, Spanish, Italian, Portuguese).
6. **Performance & Security**:
   - Zero-allocation buffer pooling (`ArrayPool<byte>`), leak-free resource disposal, safe Win32 handle encapsulation, command injection prevention, and regular vulnerability audits (`dotnet list package --vulnerable`).
7. **Consistency & Deduplication**: All new requirements must be validated against existing requirements in `REQUIREMENTS.md`.
8. **User Decision on Conflicts**: In case of contradictions or duplicates, the user must make the decision.
9. **Immutability of Existing Requirements**: Existing requirements may only be modified with explicit user instruction.
10. **100% Coverage**: 100% of code/system changes must be covered by approved requirements.
11. **Dynamic Model Allocation & Universal Execution Strategy**:
    - Model tiers and execution modes are declaratively defined in the Single Source of Truth: [`_agents/rules/model-tiers.json`](_agents/rules/model-tiers.json).
    - **Compound Phased Execution (Default Strategy)**: Core development workflows (Plan -> Inner-Loop TDD -> Verify -> Commit) execute within a **continuous conversation thread** via phased persona transitions. This preserves prefix continuity, unlocking **75–90% prompt caching / KV-cache discounts** and eliminating multi-agent spawn latency.
    - **Selective Subagent Forking (`invoke_subagent`)**: Reserved strictly for **divergent research**, broad multi-file repository exploration, web lookups, or independent background sidecars to keep exploratory token noise out of the primary thread.
    - **Terminal & Context Hygiene**: All PowerShell commands must use `-NoProfile`. Testrunners must run in quiet mode (`dotnet test --verbosity quiet`) to prevent terminal logs from bloating the context window.
    - **Sequential Persona Mode (Copilot / Cursor / Single-Model)**: Uses prompt-modulated thinking budgets (Extended for Tier 1, Low/Minimal for Tier 3/4).
12. **Branch & PR Process Model with Developer Testing & Review Gate**: All development must occur on dedicated branches (`feat/`, `fix/`, `refactor/`, `chore/`, `docs/`). Prior to Pull Request creation, the developer is provided with the opportunity to review the code, test application functionality interactively/manually, and request adjustments or fixes. Merging into `main` occurs solely via Pull Requests using Squash-and-Merge after explicit user sign-off and passing CI per [CONTRIBUTING.md](CONTRIBUTING.md).
13. **Four-Step Codebase Analysis Protocol**:
    When exploring, analyzing, or diagnosing the codebase, agents must strictly follow four progressive steps:
    1. **Check Current Modular Architecture Baseline**: Verify `ARCHITECTURE.md`, module specifications (`docs/architecture/modules/*.md`), and state checkpoint (`docs/architecture/.arch-sync.json`).
    2. **Synchronize Architecture if Needed**: If documentation is outdated or desynchronized from recent git commits, trigger `ArchitectureSync` to update affected module documents.
    3. **Deduce State from Modular Architecture Documentation**: Derive component responsibilities, public contracts, data flows, and runtime state directly from the relevant modular architecture specification (`docs/architecture/modules/<module>.md`).
    4. **Targeted Code Inspection Only for Critical Details**: Read concrete source code files strictly when specific low-level implementation details (e.g. ConPTY Win32 interop, exact event routing lines) are indispensable.
14. **Lifecycle Action Execution Governance (Commit, Push, PR Merge, Release)**:
    Defined Git and release actions adhere to six explicit execution principles:
    1. **Strict Action Execution (Atomic Scope)**: When the user requests a specific action (e.g. `commit`), execute only that action (e.g. commit only, do not automatically push).
    2. **State-Driven Prerequisite Resolution**: If the requested action requires preceding steps based on the current workspace or repository state (e.g. uncommitted changes present when `push` is requested), automatically resolve the necessary prerequisites first.
    3. **Proactive Next-Step Offering**: When an action completes and a logical subsequent step is derivable, proactively offer the user to execute it directly.
    4. **Gate Invariance**: All interactive review gates and safety validations (e.g. commit message confirmation, PR description review, SemVer release tag approval) remain mandatory and cannot be bypassed.
    5. **Explicit User Override**: The user may explicitly instruct deviating or combined behavior at any time (e.g. "commit and push directly").
    6. **Atypical State & Anomaly Gate**: If following these instructions would produce an unusual state or require non-standard/atypical measures (e.g. detached HEAD, merge conflicts, unexpected untracked files, unverified release states, cross-cutting multi-scope changes), the agent must pause, describe the situation, and prompt the user for explicit confirmation before proceeding.
15. **Adaptive Governance & Strictness Levels**:
    - `Control` establishes and propagates a `Strictness Level` context:
      - **Enterprise (Default)**: Strict adherence to Clean Architecture, 100% test coverage, and full Requirement mapping.
      - **Legacy**: Tolerates architectural deviations and missing tests (does not block verification), but requires Stage 1 compilation success.
      - **Prototype**: Focuses on speed (MVP). Architecture documentation and TDD are strictly optional.

## Subagent Roles & Governance Mappings
All 22 specialized subagent roles, their cognitive tiers, reference models, and thinking budgets are declaratively maintained in the Single Source of Truth: [`_agents/rules/model-tiers.json`](_agents/rules/model-tiers.json).
- **Core Lifecycle Roles**: `Control`, `RequirementEngineer`, `Architekt`, `Developer`, `Tester`, `Verifikation`, `CommitManager`, `PRManager`.
- **Lifecycle Specialists (On-Demand)**: `Troubleshooter`, `GitTroubleshooter`, `CodeExplainer`, `RefactoringSpecialist`, `DocumentationSpecialist`, `ArchitectureSync`, `DevOpsEngineer`, `ReleaseManager`.
- **Domain Specialists (On-Demand)**: `UIDesigner`, `LocalizationSpecialist`, `PerformanceOptimizer`, `SecurityAuditor`, `DatabaseSpecialist`, `ApiContractSpecialist`.

Operational execution instructions are defined exclusively in each role's skill specification in `_agents/skills/`.

## Context Isolation & Compaction Protocol
- Subagents must be called with only the minimum context required for their specific role.
- Intermediate results (e.g. root cause reports, UX blueprints, i18n dictionaries, architecture contracts, diffs, acceptance criteria) are passed downstream sequentially.
- No role shall receive bloated discussion history or unrelated files.
- **Proactive Context Compaction & Phase Checkpointing**:
  - `Control` executes explicit context compaction at major phase boundaries (e.g., Plan -> Implementation, or between distinct user tasks).
  - Before transitioning or starting a new task, synthesize a concise **State Checkpoint** (active goal, touched files, verified architecture facts, next concrete steps).
  - Discard obsolete intermediate trial-and-error logs, failed compilation attempts, and transient conversation history, while strictly preserving top-of-context system rules to maximize KV-cache prefix hits.

## Client Directory Compatibility (`.agents` vs. `_agents`)
- **Gemini / Antigravity**: Seamlessly supports both `_agents` and `.agents` customization roots.
- **GitHub Copilot & Other Clients**: Specifically expect `.agents/` as the standard discovery root. When sharing skills across multiple AI clients or targeting Copilot, use `.agents` (or create a symbolic link / submodule pointing to `.agents`).

Detailed skill definitions can be found in `_agents/skills/` and rules in `_agents/rules/`.
