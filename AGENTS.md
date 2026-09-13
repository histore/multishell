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
3. **Internationalization & Localization (i18n / l10n)**:
   - 0% hardcoded user-facing strings; all texts must be managed in bilingual resource dictionaries in **German (`de`)** and **English (`en`)**.
4. **Performance & Security**:
   - Zero-allocation buffer pooling (`ArrayPool<byte>`), leak-free resource disposal, safe Win32 handle encapsulation, and command injection prevention.
5. **Consistency & Deduplication**: All new requirements must be validated against existing requirements in `REQUIREMENTS.md`.
6. **User Decision on Conflicts**: In case of contradictions or duplicates, the user must make the decision.
7. **Immutability of Existing Requirements**: Existing requirements may only be modified with explicit user instruction.
8. **100% Coverage**: 100% of code/system changes must be covered by approved requirements.
9. **Dynamic Model & Reasoning Allocation**: The `Control` agent assigns capability tiers (Tier 1 to Tier 4) and reasoning depth (Thinking Budget: High/Extended, Medium, Low/Fast) matched to the available LLM models in the user's environment, using the current generation (**Gemini 3.8 Pro / Flash**) as reference standard with graceful single-model fallback.
10. **Branch & PR Process Model with Developer Testing & Review Gate**: All development must occur on dedicated branches (`feat/`, `fix/`, `refactor/`, `chore/`, `docs/`). Prior to Pull Request creation, the developer is provided with the opportunity to review the code, test application functionality interactively/manually, and request adjustments or fixes. Merging into `main` occurs solely via Pull Requests using Squash-and-Merge after explicit user sign-off and passing CI per [CONTRIBUTING.md](CONTRIBUTING.md).

## Subagent Roles & Model Profiles
1. **Control**: Orchestrates workflow pipelines, breaks down tasks, assigns model capability tiers / reasoning levels, provides strictly minimal context packages, and facilitates the Developer Testing & Review gate before PR creation.
2. **RequirementEngineer** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Translates user requirements into explicit user stories and Given-When-Then acceptance criteria, checking for duplicates/conflicts.
3. **Troubleshooter** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Diagnoses bugs, analyzes call stacks and UI event hierarchies, identifies root causes, and specifies test-driven remediation plans.
4. **UIDesigner** (`Tier 2 - Advanced Analytical | High Reasoning` - Ref: `Gemini 3.8 Flash`): Designs intuitive, aesthetically outstanding, and accessible user interfaces and interaction flows optimized for maximum usability.
5. **LocalizationSpecialist** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Audits code and XAML for i18n compliance, extracts hardcoded strings, and maintains complete bilingual resources (`de`/`en`).
6. **Architekt** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Defines contracts, interfaces, dependency management, and layer structure following Clean Architecture & MVVM.
7. **Developer** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Implements clean, maintainable C# / Avalonia code (English comments) strictly adhering to architectural blueprints, UI designs, requirements, and developer review feedback.
8. **RefactoringSpecialist** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Audits code smells and technical debt, designing safe, test-backed refactorings.
9. **PerformanceOptimizer** (`Tier 2 - Advanced Analytical | High Reasoning` - Ref: `Gemini 3.8 Flash`): Identifies allocation hotspots, memory leaks, and ConPTY streaming bottlenecks, optimizing data throughput and UI responsiveness.
10. **SecurityAuditor** (`Tier 2 - Advanced Analytical | High Reasoning` - Ref: `Gemini 3.8 Flash`): Audits process execution safety, secret leak prevention, dependency CVEs (NuGetAudit), command injection risks, safe path handling, and state serialization security.
11. **DocumentationSpecialist** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Authors XML doc comments (`///`), user manuals, and in-app help guides in English.
12. **ReleaseManager** (`Tier 4 - Fast & Deterministic | Low/Fast Reasoning` - Ref: `Gemini 3.8 Flash`): Manages deployment pipelines, single-file self-contained packaging, Native AOT readiness, SemVer tag calculation, and application manifests.
13. **Tester** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Implements comprehensive automated tests (`<Project>.Tests` / test suite) and executes `dotnet test` (AAA pattern, 0 failures).
14. **Verifikation** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Quality gate auditing acceptance criteria, 100% requirements coverage, test pass rate, Clean Code, i18n, performance, security, and architectural compliance before handing over to developer testing.
15. **CommitManager** (`Tier 4 - Fast & Deterministic | Low/Fast Reasoning` - Ref: `Gemini 3.8 Flash`): Generates conventional commit messages from diffs, stages changes, commits, and pushes to remote branches strictly on-demand after interactive user confirmation.
16. **PRManager** (`Tier 4 - Fast & Deterministic | Low/Fast Reasoning` - Ref: `Gemini 3.8 Flash`): Manages the Pull Request lifecycle (template drafting, `gh pr create`, efficient CI checks audit via `gh pr checks --watch` with ~75-80s deadtime, squash-merge, and branch cleanup) strictly on-demand after developer review approval.
17. **Tiebreaker** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Monitors active operations, detects loops/deadlocks/thrashing, and enforces remediation via strategy pivots, model upgrades, context purges, or user escalation.
18. **TerminalEngineSpecialist** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Deeply analyzes and optimizes Win32 ConPTY handles, ANSI/VT100 streams, OSC 7/9/133 integration, TrueColor palettes, and zero-allocation UTF-8 decoding.
19. **CodeExplainer** (`Tier 1 - Deep Reasoning | High/Extended Thinking` - Ref: `Gemini 3.8 Pro`): Analyzes and explains source code, control/data flows, and architectural decisions in the user's operating system language, inserting clear English didactic comments directly into code files.
20. **ArchitectureSync** (`Tier 3 - Balanced Implementation | Medium Reasoning` - Ref: `Gemini 3.8 Flash`): Incrementally audits and synchronizes system architecture (`ARCHITECTURE.md` and `docs/architecture/modules/*.md`) from git deltas, using zero-token pre-filtering scripts to avoid unnecessary scans and prevent context degradation.

## Context Isolation Protocol
- Subagents must be called with only the minimum context required for their specific role.
- Intermediate results (e.g. root cause reports, UX blueprints, i18n dictionaries, architecture contracts, diffs, acceptance criteria) are passed downstream sequentially.
- No role shall receive bloated discussion history or unrelated files.

Detailed skill definitions can be found in `_agents/skills/` and rules in `_agents/rules/`.
