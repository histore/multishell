# Subagent Orchestration, Clean Architecture & Context Isolation Guidelines

## Core Principles
1. **Isolated Context**: Each subagent role operates within an isolated task context to prevent context bloat and distraction.
2. **Minimal Context Transfer**: Only essential information (inputs, specific requirements, direct dependencies) is passed between roles.
3. **Dynamic Model & Reasoning Assignment**: The `Control` agent assigns appropriate LLM models and reasoning levels tailored to each role's cognitive demands.
4. **Clean Architecture & Clean Code Enforcement**:
   - **Clean Architecture**: Dependency rule (dependencies point inward), clear layer boundaries (`Models`, `Services`, `ViewModels`, `Views`), independent of external UI/OS details.
   - **Clean Code**: SOLID, DRY, KISS, YAGNI, Boy Scout Rule, small focused classes/methods, descriptive naming, English comments.
   - **Modern Best Practices**: C# 13 / .NET 10 idioms (file-scoped namespaces, nullability `#nullable enable`, records, collection expressions, pattern matching, async/await with `ConfigureAwait(false)` in service layers, safe native handle disposal), Avalonia UI 11.2 (compiled bindings `x:DataType`, CommunityToolkit.Mvvm source generators, decoupled styles).
5. **Systematic Root Cause Analysis**:
   - The `Troubleshooter` diagnoses bugs, analyzes event hierarchies and call stacks, and isolates root causes before code changes occur.
6. **Full Internationalization (i18n & l10n)**:
   - Dedicated `LocalizationSpecialist` role audits and enforces 0% hardcoded UI strings, managing bilingual German (`de`) and English (`en`) resource dictionaries.
7. **Performance, Security & Code Health**:
   - `PerformanceOptimizer` enforces zero-allocation buffer pooling (`ArrayPool<byte>`) and memory leak prevention.
   - `SecurityAuditor` audits command safety, path traversal prevention, and secure JSON deserialization.
   - `RefactoringSpecialist` continuously eliminates technical debt and code smells.
   - `DocumentationSpecialist` maintains XML doc comments (`///`) and architecture documents.
   - `ReleaseManager` manages packaging, self-contained single-file publishing, and manifests.
8. **Strict Requirements Governance**:
   - **Check Against Existing Requirements**: Every new requirement must be validated against `REQUIREMENTS.md`.
   - **User Decision on Conflicts/Duplicates**: Contradictions or duplicates must be escalated to the user for explicit decision.
   - **Immutability of Existing Requirements**: Existing requirements may only be modified with explicit user instruction.
   - **Full Coverage**: 100% of code/system changes must be covered by approved requirements.
9. **Maximum User Usability & Aesthetic Excellence**: The dedicated `UIDesigner` role ensures every UI component provides effortless keyboard navigation, intuitive ergonomics, and rich visual aesthetics.
10. **Strict Stage Gating**: Work flows sequentially through defined checkpoints before acceptance.

---

## Defined Roles & Workflow Pipelines

### Feature Development Pipeline
```
[User Feature Request]
       │
       ▼
   [Control] (Assigns Models & Reasoning Levels) ◄──────────────────────┐
       │ (delegates with minimal isolated context)                       │
       ├──► [RequirementEngineer]   [Reasoning: High]                   │
       │     └──► Acceptance Criteria (checks duplicates/conflicts)     │
       │                                                                │
       ├──► [UIDesigner]            [Reasoning: High]                   │
       │     └──► Ergonomics, Layout & Aesthetic XAML Blueprint         │
       │                                                                │
       ├──► [LocalizationSpecialist][Reasoning: Medium]                 │
       │     └──► i18n Audit & Bilingual Resources (de/en)              │
       │                                                                │
       ├──► [Architekt]             [Reasoning: High]                   │
       │     └──► Clean Architecture & Interface Contracts              │ (Iterate / Gate)
       │                                                                │
       ├──► [Developer]             [Reasoning: Medium]                 │
       │     └──► Clean Code Implementation                             │
       │                                                                │
       ├──► [DocumentationSpecialist][Reasoning: Medium]                │
       │     └──► XML Doc Comments & Architecture Sync                  │
       │                                                                │
       ├──► [Tester]                [Reasoning: Medium]                 │
       │     └──► Automated Unit & Integration Tests (AAA)              │
       │                                                                │
       └──► [Verifikation]          [Reasoning: High]                   │
             └──► Quality Gate / Compliance Audit ──────────────────────┘
```

### Bug Fixing & Troubleshooting Pipeline
```
[Bug Report / Unexpected Behavior]
       │
       ▼
   [Control] ──► [Troubleshooter] [Reasoning: High]
                      │ (Root Cause Diagnosis & Remediation Blueprint)
                      ├──► [Tester]    ──► Failing Reproduction Test
                      ├──► [Developer] ──► Root Cause Clean Code Fix
                      └──► [Verifikation] ──► Pass Verification & Quality Gate
```

### Performance & Security Hardening Pipeline
```
[Optimization / Hardening Task]
       │
       ▼
   [Control] ──► [PerformanceOptimizer / SecurityAuditor] [Reasoning: High]
                      │ (Optimization Directives / Security Patch Blueprint)
                      ├──► [Developer] ──► Implementation
                      ├──► [Tester]    ──► Benchmarks & Regression Tests
                      └──► [Verifikation] ──► Pass Verification
```

---

## Role Summary & Profiles

1. **Control**: Central workflow orchestrator, model/reasoning dispatcher, and minimal-context packager.
2. **RequirementEngineer** (`Reasoning: High`): Given-When-Then criteria, conflict detection, requirement integrity.
3. **Troubleshooter** (`Reasoning: High`): RCA, stack trace analysis, event bubbling & race condition diagnostics.
4. **UIDesigner** (`Reasoning: High`): Usability, keyboard workflows, visual aesthetics (Catppuccin Mocha), XAML styles.
5. **LocalizationSpecialist** (`Reasoning: Medium`): i18n audits, 0% hardcoded strings, bilingual dictionaries (`de`/`en`).
6. **Architekt** (`Reasoning: High`): Clean Architecture, SOLID interfaces, layer boundaries, dependency inversion.
7. **Developer** (`Reasoning: Medium`): Clean Code implementation, C# 13, .NET 10, Avalonia 11.2, English comments.
8. **RefactoringSpecialist** (`Reasoning: Medium`): Code smell detection, technical debt remediation, Boy Scout rule.
9. **PerformanceOptimizer** (`Reasoning: High`): Buffer pooling (`ArrayPool`), memory leak auditing, ConPTY stream throughput.
10. **SecurityAuditor** (`Reasoning: High`): PowerShell command safety, path traversal prevention, secure deserialization.
11. **DocumentationSpecialist** (`Reasoning: Medium`): English XML doc comments, `ARCHITECTURE.md`, help manual synchronization.
12. **ReleaseManager** (`Reasoning: Medium`): Self-contained single-file publishing, AOT readiness, manifest management.
13. **Tester** (`Reasoning: Medium`): Automated xUnit tests in `MultiShell.Tests`, AAA pattern, 0 failures.
14. **Verifikation** (`Reasoning: High`): Rigorous quality gate auditing 100% requirements coverage, Clean Architecture, i18n, performance, and security.
