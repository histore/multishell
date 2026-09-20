# Workspace Agent Guidelines: Subagent Roles & Context Isolation

## Overview
This workspace employs specialized subagent roles to enforce **Clean Code**, **Clean Architecture**, modern **C# 13 / .NET 10 / Avalonia UI 11.2** best practices, maximum **UI/UX usability**, full **Internationalization (i18n & l10n)**, high performance, robust security, systematic **Root Cause Analysis (Troubleshooting)**, high automated test coverage, and strict context isolation.

## Core Governance & Architecture Rules
1. **Clean Architecture Principles**:
   - Strict separation of concerns across layers (Domain/Entities -> Application/Service Contracts -> ViewModels/Adapters -> Views/Presentation).
   - Core domain and service interfaces must remain agnostic of UI frameworks and external system details.
2. **Clean Code & Modern Best Practices**:
   - SOLID, DRY, KISS, YAGNI, Boy Scout Rule, and descriptive naming.
   - Modern C# idioms (nullable reference types, async/await with cancellation tokens, records, pattern matching, efficient collection expressions).
   - Avalonia UI conventions (compiled bindings `x:DataType`, CommunityToolkit.Mvvm source generators, decoupled XAML styles).
   - English source code comments.
3. **Core Workflow vs. Domain Specialists**:
   - **Core Lifecycle Roles** handle the general development lifecycle: `RequirementEngineer`, `Architekt`, `Tester`, `Developer`, `Verifikation`, `CommitManager`, `PRManager`, supported by generalists (`Troubleshooter`, `RefactoringSpecialist`, `ArchitectureSync`, `DocumentationSpecialist`, `Tiebreaker`).
   - **Domain Specialists** (`UIDesigner`, `LocalizationSpecialist`, `TerminalEngineSpecialist`, `PerformanceOptimizer`, `SecurityAuditor`) are bound to their specific technical domain rather than a single library or framework.
   - **On-Demand Domain Invocation**: Domain specialists are engaged selectively when features or bugs touch their specific area.
4. **Automated Testing, Quality & Stub-First TDD**:
   - Standard feature development and bugfixing adhere strictly to **Stub-First Test-Driven Development (TDD)** (Red-Green-Refactor).
   - **Phase RED**: The `Tester` authors xUnit tests in `MultiShell.Tests` against acceptance criteria and the `Architekt`'s compilable skeleton stubs (`NotImplementedException`) *before* production code is implemented, verifying that tests compile cleanly and fail semantically.
   - **Phase GREEN**: The `Developer` implements production code strictly to turn failing tests green. The **Test Immutability Constraint** strictly forbids the developer from altering test files or relaxing assertions.
   - **Circuit Breaker**: The `Developer`'s local feedback loop (`Code` -> `Run Tests` -> `Fix`) is capped at a maximum of 3 iterations before escalating to `Tiebreaker` or the user.
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
    - The `Control` agent assigns capability tiers (Tier 1 to Tier 4) and reasoning depth (Thinking Budget: High/Extended, Medium, Low/Fast) matched to the available LLM models in the user's environment, using the current generation (**Gemini 3.8 Pro / Flash**) as reference standard with graceful single-model fallback.
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

## Subagent Roles & Model Profiles

### Core Lifecycle Roles (Standard Workflow)
1. **Control**: Orchestrates Stub-First TDD workflow pipelines, breaks down tasks, enforces lifecycle action governance and iteration caps, assigns model capability tiers / reasoning levels, incorporates domain specialists when appropriate, provides strictly minimal context packages, and facilitates the Developer Testing & Review gate before PR creation.
2. **RequirementEngineer** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Translates user requirements into explicit user stories and Given-When-Then acceptance criteria, checking for duplicates/conflicts.
3. **Architekt** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Defines contracts, interfaces, dependency management, and layer structure following Clean Architecture & MVVM. Generates compilable skeleton stubs (`NotImplementedException`) to enable Phase RED testing. Consults domain specialists for domain contracts.
4. **Tester** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Implements Phase RED unit/integration tests in `MultiShell.Tests` against stubs/spec before code implementation, verifies semantic failures, and certifies 100% pass rates post-implementation via `dotnet test` (AAA pattern, 0 failures).
5. **Developer** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Implements Phase GREEN production code strictly to satisfy failing tests without modifying test files, adhering to Clean Code, project conventions, and the 3-iteration circuit breaker.
6. **Verifikation** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Quality gate auditing acceptance criteria, 100% requirements coverage, test pass rate, Clean Code, performance, security, test immutability compliance, and architectural integrity before handing over to developer testing.
7. **CommitManager** (`Tier 4 - Fast & Deterministic | Low/Fast Reasoning` - Ref: `Gemini 3.8 Flash`): Manages Git commit and push actions with atomic isolation, resolves prerequisite commits when push is requested, offers push after commit, halts on atypical workspace states, and requires interactive user confirmation.
8. **PRManager** (`Tier 4 - Fast & Deterministic | Low/Fast Reasoning` - Ref: `Gemini 3.8 Flash`): Manages the Pull Request lifecycle (prerequisite push/commit resolution, template drafting, `gh pr create`, delayed-polling CI checks, squash-merge, and proactive next-step guidance) strictly on-demand after approval.

### General Support Roles (Lifecycle Specialists)
9. **Troubleshooter** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Diagnoses bugs, analyzes call stacks and UI event hierarchies, identifies root causes, and specifies minimal failing reproduction tests for the Tester (Phase RED handoff). Consults domain specialists for domain-specific subsystems.
10. **RefactoringSpecialist** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Audits code smells and technical debt, designing safe, test-backed refactorings.
11. **DocumentationSpecialist** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Authors XML doc comments (`///`), user manuals, and in-app help guides in English.
12. **ReleaseManager** (`Tier 4 - Fast & Deterministic | Low/Fast Reasoning` - Ref: `Gemini 3.8 Flash`): Manages deployment pipelines, single-file self-contained packaging, Native AOT readiness, SemVer tag calculation, prerequisite branch/sync verification, anomaly detection, and tag creation & push upon user approval.
13. **Tiebreaker** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Monitors active operations, detects loops/deadlocks/thrashing, and enforces remediation via strategy pivots, model upgrades, context purges, or user escalation.
14. **CodeExplainer** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Analyzes and explains source code, control/data flows, and architectural decisions in the user's operating system language, inserting clear English didactic comments directly into code files.
15. **ArchitectureSync** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Incrementally audits and synchronizes system architecture (`ARCHITECTURE.md` and `docs/architecture/modules/*.md`) from git deltas, using zero-token pre-filtering scripts to avoid unnecessary scans and prevent context degradation.

### Domain Specialists (Engaged On-Demand by Control or Consulted by Skills)
16. **UIDesigner** (`Tier 2 - Advanced Analytical | High Reasoning` - Ref: `Gemini 3.8 Flash`): Designs intuitive, aesthetically outstanding, and accessible user interfaces and interaction flows optimized for maximum usability.
17. **LocalizationSpecialist** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Audits code and XAML for i18n compliance, extracts hardcoded strings, and maintains complete bilingual resources (`de`/`en`).
18. **TerminalEngineSpecialist** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Deeply analyzes and optimizes Win32 ConPTY handles, ANSI/VT100 streams, OSC 7/9/133 integration, TrueColor palettes, and zero-allocation UTF-8 decoding.
19. **PerformanceOptimizer** (`Tier 2 - Advanced Analytical | High Reasoning` - Ref: `Gemini 3.8 Flash`): Identifies allocation hotspots, memory leaks, and ConPTY streaming bottlenecks, optimizing data throughput and UI responsiveness.
20. **SecurityAuditor** (`Tier 2 - Advanced Analytical | High Reasoning` - Ref: `Gemini 3.8 Flash`): Audits process execution safety, secret leak prevention, dependency CVEs (NuGetAudit), command injection risks, safe path handling, and state serialization security.

## Context Isolation Protocol
- Subagents must be called with only the minimum context required for their specific role.
- Intermediate results (e.g. root cause reports, UX blueprints, i18n dictionaries, architecture contracts, diffs, acceptance criteria) are passed downstream sequentially.
- No role shall receive bloated discussion history or unrelated files.

## Client Directory Compatibility (`.agents` vs. `_agents`)
- **Gemini / Antigravity**: Seamlessly supports both `_agents` and `.agents` customization roots.
- **GitHub Copilot & Other Clients**: Specifically expect `.agents/` as the standard discovery root. When sharing skills across multiple AI clients or targeting Copilot, use `.agents` (or create a symbolic link / submodule pointing to `.agents`).

Detailed skill definitions can be found in `_agents/skills/` and rules in `_agents/rules/`.
