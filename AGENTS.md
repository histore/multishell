# MultiShell Project Guidelines: Subagent Governance & Architecture

## Universal Multi-Agent Governance
This workspace adheres to multi-agent governance, role definitions, execution strategies, and lifecycle safety rules based on [histore/agent-skills](https://github.com/histore/agent-skills):
- Clean Architecture layer separation
- Inner-Loop TDD profiles (Profile A Fast-Track, Profile B Standard, Profile C Complex)
- Two-Stage Quality Gates
- Lifecycle Action Governance with Adaptive Gate Resolution and Clean Release Tagging Invariant
- 4-step codebase analysis protocol, context compaction, and KV-cache optimization

---

## Project-Specific Stack & Environment (MultiShell)

### 1. Platform & Toolchain
- **Target OS**: Windows (Win32 ConPTY API integration)
- **Primary Shell**: PowerShell (`pwsh -NoProfile`)
- **Runtime & Language**: .NET 10 / C# 13 (nullable reference types enabled)
- **UI Framework**: Avalonia UI 12.1 (Desktop)
- **MVVM Framework**: CommunityToolkit.Mvvm (source generators)
- **Test Runner**: Native .NET CLI in quiet mode (`dotnet test --verbosity quiet`)

### 2. Clean Architecture & Solution Structure
The solution is strictly layered according to Clean Architecture (structured under `MultiShell.csproj` with namespace and directory separation per `ARCHITECTURE.md`):
- **Domain Layer (`Models/`)**:
  - Agnostic of UI frameworks and external system details.
  - Contains entity models, value objects, serialization records (`TabState`, `TerminalProfile`, `LanguageOption`, `PathColorStripe`, `ShellType`).
- **Services Layer (`Services/`)**:
  - Low-level Win32 P/Invoke interop for Windows Pseudo Console (ConPTY) using source-generated `[LibraryImport]`.
  - Safe Win32 handle encapsulation (`SafeFileHandle`, `SafeProcessHandle`) with leak-free disposal.
  - Application services (`TerminalProfileService`, `LocalizationService`, `TabStatePersistenceService`, `FuzzySearchService`, `DirectoryHistoryService`, etc.).
- **Presentation Layer (`ViewModels/` & `Views/`)**:
  - UI-agnostic view models (`MainViewModel`, `TerminalTabViewModel`) leveraging `[ObservableProperty]` and `[RelayCommand]`.
  - Avalonia UI XAML views (`MainWindow`, `TerminalTabView`, dialogs) using compiled bindings (`x:DataType`).
  - Terminal tab coordination, session history, and command routing.
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
