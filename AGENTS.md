# MultiShell Project Guidelines: Subagent Governance & Architecture

## Universal Multi-Agent Governance
This workspace adheres to the multi-agent governance, role definitions, execution strategies, and lifecycle safety rules defined in the central agent framework:
- **Core Governance & Workflow Rules**: [`_agents/AGENTS.md`](_agents/AGENTS.md)
- **Role & Model Tier Allocations**: [`_agents/rules/model-tiers.json`](_agents/rules/model-tiers.json)
- **Skill Definitions**: [`_agents/skills/`](_agents/skills/)

For universal principles (Clean Architecture layer separation, Inner-Loop TDD profiles A/B/C, Two-Stage Quality Gates, Lifecycle Action Governance with Adaptive Gate Resolution, 4-step codebase analysis protocol, context compaction, and KV-cache optimization), refer strictly to [`_agents/AGENTS.md`](_agents/AGENTS.md).

---

## Project-Specific Stack & Environment (MultiShell)

### 1. Platform & Toolchain
- **Target OS**: Windows (Win32 ConPTY API integration)
- **Primary Shell**: PowerShell (`pwsh -NoProfile`)
- **Runtime & Language**: .NET 10 / C# 13 (nullable reference types enabled)
- **UI Framework**: Avalonia UI 11.2 (Desktop)
- **MVVM Framework**: CommunityToolkit.Mvvm (source generators)
- **Test Runner**: Native .NET CLI in quiet mode (`dotnet test --verbosity quiet`)

### 2. Clean Architecture & Solution Structure
The solution is strictly layered according to Clean Architecture:
- **`MultiShell.Core` (Domain & Contracts)**:
  - Agnostic of UI frameworks and external system details.
  - Contains entity models, service interfaces, state machine contracts, and domain events.
- **`MultiShell.Engine` (Infrastructure / Interop / Drivers)**:
  - Low-level Win32 P/Invoke interop for Windows Pseudo Console (ConPTY).
  - Safe Win32 handle encapsulation (`SafeFileHandle`, `SafeProcessHandle`) with leak-free disposal.
  - Pipe streaming, buffer management, and process lifecycle management.
- **`MultiShell.ViewModels` (Application & Presentation Logic)**:
  - UI-agnostic view models leveraging `[ObservableProperty]` and `[RelayCommand]`.
  - Terminal tab coordination, session history, and command routing.
- **`MultiShell.UI` / `MultiShell.Desktop` (Presentation Layer)**:
  - Avalonia UI XAML views and custom renderers.
  - Strict compiled bindings (`x:DataType`) on all controls and data templates.
  - Decoupled XAML style dictionaries and theme assets.
- **`MultiShell.Tests` (Test Suite)**:
  - Automated unit and integration tests following the Arrange-Act-Assert (AAA) pattern.
  - 100% pass rate requirement with zero failing tests.

### 3. Coding Conventions & Best Practices
- **Idiomatic Modern C# 13**:
  - Extensive use of nullable reference types, pattern matching, records for immutable state/events, and collection expressions (`[...]`).
  - Asynchronous workflows must accept and propagate `CancellationToken`.
- **Clean Code**:
  - SOLID, DRY, KISS, YAGNI, Boy Scout Rule, descriptive naming.
  - All source code comments, documentation comments (`///`), and docstrings must be written in **English**.
- **Avalonia UI Standards**:
  - Use compiled bindings `x:DataType` everywhere; avoid untyped reflection bindings.
  - Keep code-behind minimal; drive UI logic entirely via ViewModels and attached behaviors.

### 4. Internationalization (i18n & l10n)
- **0% hardcoded user-facing strings**: All UI labels, tooltips, dialogs, and messages must be stored in dynamic resource dictionaries.
- **Supported Languages**: German (`de`) and English (`en`) as the primary bilingual baseline (expandable to French, Spanish, Italian).
- Resources are dynamically switchable at runtime without application restart.

### 5. High Performance & Security Guardrails
- **Zero-Allocation Stream Processing**:
  - ConPTY input/output streams must use buffer pooling (`ArrayPool<byte>.Shared`) to prevent GC thrashing during heavy terminal output.
- **Safe Native Interop**:
  - All native Win32 handles must be encapsulated in safe handles (`SafeHandleZeroOrMinusOneIsInvalid`).
  - No unmanaged memory leaks across terminal create/resize/close lifecycles.
- **Security & Injection Defense**:
  - Safe process startup parameters; command arguments must never be passed to a raw command interpreter without sanitization.
  - Regular dependency audits (`dotnet list package --vulnerable`).

### 6. Architecture & Requirements Baselines
- **Architecture Baseline**:
  - Central Hub: [`ARCHITECTURE.md`](ARCHITECTURE.md)
  - Modular Specs: [`docs/architecture/modules/*.md`](docs/architecture/modules/)
  - State Checkpoint: [`docs/architecture/.arch-sync.json`](docs/architecture/.arch-sync.json)
- **Requirements Baseline**:
  - Central Hub & Registry: [`REQUIREMENTS.md`](REQUIREMENTS.md)
  - Modular Requirements: [`docs/requirements/modules/*.md`](docs/requirements/modules/)
