# MultiShell Architecture & Design

## System Purpose & Scope
MultiShell is a high-performance, multi-tab terminal emulator built with **C# 13**, **.NET 10.0**, and **Avalonia UI 11.2** for Windows. It provides direct Win32 ConPTY virtualization, zero-allocation UTF-8 stream processing, persistent workspaces, multi-shell profile management (PowerShell 7, Windows PowerShell, WSL, CMD, NuShell), and live fuzzy search history drawers with dynamic i18n support.

For the comprehensive system architecture specification and subsystem diagrams, see [docs/architecture/overview.md](docs/architecture/overview.md).

## Architecture & Layers
MultiShell strictly adheres to **Clean Architecture** and **MVVM** principles:

- **Domain Layer (`Models/`)**: Core immutable entities, value objects, and serialization records (`TabState`, `TerminalProfile`, `LanguageOption`) with zero external dependencies.
- **Services Layer (`Services/`)**: Application contracts and infrastructure implementations (`ShellSession` for ConPTY Win32 pipes, `TerminalProfileService`, `ThemeService`, `LocalizationService`, `TabStatePersistenceService`, `FuzzySearchService`, `PathCommandHistoryService`).
- **Presentation Layer (`ViewModels/` & `Views/`)**: Reactive view models (`MainViewModel`, `TerminalTabViewModel`) using `CommunityToolkit.Mvvm`, decoupled from Avalonia UI controls, and XAML views using compiled bindings (`x:DataType`).
- **Automated Test Suite (`MultiShell.Tests/`)**: Comprehensive xUnit tests adhering to the AAA pattern.

### Core Architectural Invariants
1. **Inward Dependencies**: Dependencies point strictly inward. Domain models and service contracts remain agnostic of UI controls.
2. **Framework Decoupling**: ViewModels contain presentation logic and observable state without referencing concrete UI controls (`Window`, `Control`, `Visual`).
3. **Compiled Bindings**: All Avalonia XAML views use compiled bindings (`x:DataType`) for compile-time type safety and peak runtime performance.
4. **Bilingual & Dynamic i18n**: 0% hardcoded strings in UI/XAML; all strings are resolved dynamically through `LocalizationService` (English, German, French, Spanish, Italian, Portuguese).
5. **Safe Native Interop**: Low-level Win32 ConPTY and kernel32 pipe handles are safely wrapped in `SafeHandle` instances with leak-free disposal lifecycles.

## Cross-Cutting Concerns
- **Concurrency & ConPTY Streaming**: Asynchronous ConPTY stdout/stderr reading with stateful UTF-8 chunk decoding (`Decoder.GetChars`) and real-time OSC 7/9/133 shell integration sequence parsing.
- **Process Codepage & Fonts**: Process-wide UTF-8 manifest ([`app.manifest`](app.manifest)), console codepage 65001, and cross-platform monospace font fallback chain (`Cascadia Code NF`, `Cascadia Mono NF`, etc.).
- **Atomic Persistence**: Thread-safe atomic JSON workspace persistence (`%LOCALAPPDATA%/MultiShell/tabs_state.json`) with safe temporary swap files.

## Modules Index
Detailed technical specifications and design blueprints are modularized and maintained incrementally under [docs/architecture/modules/](docs/architecture/modules/):

| Module | Specification | Scope & Key Responsibilities |
| :--- | :--- | :--- |
| **Terminal Session & ConPTY** | [`terminal-session.md`](docs/architecture/modules/terminal-session.md) | Win32 ConPTY lifecycle, pipe redirection, stateful UTF-8 decoding, OSC 7/9/133 shell integration. |
| **Presentation & MVVM** | [`presentation.md`](docs/architecture/modules/presentation.md) | Avalonia UI composition, `MainViewModel` partials, `TerminalTabViewModel`, tab drag/drop, persistent panels. |
| **Profiles & Configuration** | [`profiles-and-configuration.md`](docs/architecture/modules/profiles-and-configuration.md) | Shell profile detection, default profile seeding, JSON profile store. |
| **Workspace Persistence** | [`persistence.md`](docs/architecture/modules/persistence.md) | Session serialization (`tabs_state.json`), atomic temp-swap writing, AOT-compliant System.Text.Json context. |
| **Internationalization (i18n)** | [`localization.md`](docs/architecture/modules/localization.md) | 0% hardcoded strings, dynamic runtime language switching (EN, DE, FR, ES, IT, PT), fallback handling. |
| **Theming, Palettes & Fonts** | [`theming-and-styling.md`](docs/architecture/modules/theming-and-styling.md) | Avalonia theme variants (Dark/Light), 16-color ANSI & 24-bit TrueColor palettes, dynamic font scaling. |
| **Fuzzy Search & Drawer** | [`search-and-drawer.md`](docs/architecture/modules/search-and-drawer.md) | Subsequence fuzzy matching, slide-out History Drawer, path-bound dynamic command history, interactive URL/path link detection. |

### Architectural Decision Records (ADR)
Architectural decisions, rationale, and trade-offs are documented under [docs/architecture/adr/](docs/architecture/adr/).

### Incremental Synchronization
System architecture documentation is kept in sync with ongoing code changes via the `la-architecture-sync` skill using git revision checkpoints ([`docs/architecture/.arch-sync.json`](docs/architecture/.arch-sync.json)).
