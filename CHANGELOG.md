# Changelog

All notable changes to this project will be documented in this file.

## [v0.28.0] - 2026-11-19

### Added
- Add Italian (`it`) and Portuguese (`pt`) installer languages to `installer.iss` (`BL-LOW-003`).
- Package `LICENSE` and `THIRD_PARTY_NOTICES.md` into build output and publish archives (`BL-MED-006`), and publish framework-dependent single-file zip archive in `release.yml` (`REQ-REL-003`, `BL-HIGH-007`).

### Changed
- Migrate all remaining Win32 P/Invoke declarations to `[LibraryImport]`, decouple `ThemeService`, and centralize UI service composition defaults (`BL-MED-007`).
- Migrate hot-path OSC, ANSI, and URL regexes to source-generated `[GeneratedRegex]` and add an ESC-free fast-path in `ShellSession.Streams` (`BL-LOW-001`).
- Extract shared `StoragePickerHelper` and `ClipboardHelper` in `Views/` and deduplicate string-history filtering in `TerminalTabViewModel.History.cs` (`BL-LOW-005`).

### Fixed
- Fix split-pane initial session lifecycle and disposal leak (`BL-CRIT-001`) and route in-terminal search overlay commands to the active split pane (`BL-CRIT-002`).
- Fix `ShellSession` ConPTY handle leaks, disposal races, and locked OSC callback invocations (`BL-HIGH-002`), and marshal execution state updates to the UI thread (`BL-HIGH-003`).
- Prevent event and visual-tree leaks across tab and split-pane re-attachment (`BL-HIGH-004`), guard `async void` UI handlers, prevent duplicate tab archiving, and forward satellite window DI dependencies (`BL-HIGH-005`).
- Preserve reflected `SvcSystems.UI.Terminal` and `XTerm.NET` members for NativeAOT trimming (`BL-HIGH-006`) and define missing theme resources and localized secondary window titles (`BL-HIGH-007`).
- Prevent persistence `.tmp` file collisions, debounce workspace saves, and guard async profile loading (`BL-MED-001`).
- Prune evicted commands from global history (`BL-MED-004`), define `Button.tabMenuItemBtn` style, localize remaining UI strings (`BL-MED-005`), and replace bare catch blocks with targeted exception filters (`BL-MED-008`).
- Dispose `Process.Start` handles, resolve `explorer.exe` via `%SystemRoot%` (`BL-LOW-002`), prune 16 unused i18n keys, and harmonize localized modifier names (`BL-LOW-003`).
- Use monotonic `_tabCounter` for new tab titles and cache zoomed `LeafPaneNodeViewModel` in `DisplayRootNode` (`BL-LOW-006`).

### Security
- Harden UNC link validation, tab CWD priority, `file://` URI handling, and bracketed paste sanitization (`BL-HIGH-001`).
- Harden `SingleInstanceService` named-pipe framing, client ACL/impersonation security, and shutdown ordering (`BL-MED-002`).
- Prioritize `System32` for built-in shells and apply Win32 argument escaping to custom executables (`BL-MED-003`).

## [v0.27.4] - 2026-11-13

### Changed
- Decouple ViewModels from Avalonia and `TerminalControlModel` via `IDispatcherService` and `ITerminalModelAdapter` (`BL-ARCH-001`).
- Replace broad silent catch blocks with targeted exception filters (`BL-QUAL-001`), use `ProcessStartInfo.ArgumentList` in `LinkDetectionHelper` (`BL-QUAL-002`), and consolidate streaming/UI-feed buffering via `TerminalUiFeedCoordinator` (`BL-QUAL-003`).

## [v0.27.3] - 2026-11-12

### Fixed
- Enforce UI-thread marshaling, timer reuse, and Win32 quote escaping across ViewModels and Services.

## [v0.27.2] - 2026-11-12

### Fixed
- Fix event subscription leaks, WSL root directory quoting, and localized execution status badges.

## [v0.27.1] - 2026-11-12

### Changed
- Decouple `PathColorCodingService` from Avalonia types, modularize ViewModels and Views under 500 lines, and enforce C# 13 `Lock` primitives.

## [v0.27.0] - 2026-11-10

### Added
- Multi-window tab undocking (`Ctrl+Shift+U`), cross-window tab drag & drop, and primary/secondary window lifecycle coordination (`REQ-TAB-026`).

## [v0.26.1] - 2026-11-02

### Fixed
- Align terminal link and file path hover highlight precisely to text glyph metrics, correcting upward vertical drift and leftward side-bearing offset (`REQ-TERM-005`).

## [v0.26.0] - 2026-10-31

### Added
- Multi-instance CLI support (`--multi-instance`, `-m`) and legacy named pipe fallback for single-instance IPC.

### Security
- Mask sensitive credentials in shell command history before storage (`QLT-02`).
- Prevent binary planting and handle trailing backslash quote escaping in shell launches (`SEC-04`, `SEC-05`).
- Harden single-instance IPC with user isolation and server-side path validation (`SEC-03`).
- Harden link detection against dangerous script execution and UNC abuse (`SEC-02`).
- Prevent PowerShell code injection in toast notifications (`SEC-01`).

## [v0.25.3] - 2026-10-28

### Fixed
- Prevent rapid clicks on overflow scroll buttons from triggering empty-space tab creation (`REQ-TAB-022`, `REQ-TAB-014`).

## [v0.25.2] - 2026-10-23

### Added
- Real-time process execution status in Tab Switcher overlay (`REQ-TAB-027`), displaying dynamic shell profile icons, process state glyphs (running, waiting for input, completed/failed exit code), and output activity pulse with tab accent and Padovan color stripe visual parity.

## [v0.25.1] - 2026-10-21

### Fixed
- Resolve IL2075 AOT trimming warnings in `SuppressMouseTrackingMode`.

## [v0.25.0] - 2026-10-21

### Added
- 7-level font scaling (10pt to 22pt with 14pt default) configurable via Settings and keyboard shortcuts (`Ctrl++`, `Ctrl+-`, `Ctrl+0`).
- Independent App UI (Dark / Light) and Terminal Shell (Dark / Light) theme switching.

## [v0.24.6] - 2026-10-21

### Fixed
- Support PageUp/PageDown buffer scrolling and multiline Shift+Enter / Ctrl+Enter routing in terminal split panes.

## [v0.24.5] - 2026-10-20

### Documentation
- Synchronize modular architecture specifications and contracts with repository HEAD.

## [v0.24.4] - 2026-10-20

### Fixed
- Allow deleting individual items from the History Drawer and auto-prune non-existent directories on drawer open.

## [v0.24.3] - 2026-10-20

### Fixed
- Prioritize newer entries on equal match quality in fuzzy history search and fix history restoration ordering.

## [v0.24.2] - 2026-10-20

### Fixed
- Ensure active input focus on tab switch and add bracketed paste mode support in Copilot CLI.

## [v0.24.1] - 2026-10-19

### Fixed
- Fix AltGr key handling on international keyboards, clipboard shortcuts, and shift-selection bypass in TUI full-screen mode.

## [v0.24.0] - 2026-10-18

### Added
- Terminal Split Panes (`REQ-UI-006`) with horizontal and vertical splits, draggable splitters, recursive binary-tree pane nodes, and Native AOT packaging support.

## [v0.23.2] - 2026-10-14

### Fixed
- Resolve i18n key gaps, ConPTY lifecycle disposal leak prevention, and zero-allocation stream buffer pooling (`ArrayPool<byte>.Shared`).

## [v0.23.1] - 2026-10-14

### Fixed
- Resolve persistent failed status indicator on tab headers after subsequent successful commands.

## [v0.23.0] - 2026-10-12

### Added
- Disable parent breadcrumbs during active processes and add orange waiting indicator for interactive prompts.

## [v0.22.0] - 2026-10-10

### Added
- Desktop toast notifications on completion of long-running background tasks (`REQ-TERM-012`).
- Status bar outside terminal area with native Caps Lock indicator and interactive breadcrumbs.

## [v0.21.1] - 2026-10-09

### Fixed
- Normalize file browser paths and sanitize argument quoting for Windows File Explorer.

## [v0.21.0] - 2026-10-09

### Added
- Shortcut (`Ctrl+Shift+E`) and tab context menu action to open current tab working directory in Windows File Explorer.

## [v0.20.6] - 2026-10-08

### Fixed
- Resolve exponential `PropertyChanged` event subscription leak in `HistoryDrawerView`.

## [v0.20.5] - 2026-10-08

### Fixed
- Resolve memory spike on tab switch and memory leak on tab close; switch PTY chunk UI dispatch priority to Normal and trim StringBuilder buffer after bursts.

## [v0.20.4] - 2026-10-07

### Added
- Headless UI tests and ViewModel history search intent.

## [v0.20.3] - 2026-10-07

### Refactor
- Modularize localization dictionaries (DE, EN, FR, ES, IT, PT), modular shell session, and synchronize architecture specifications.

## [v0.20.2] - 2026-10-07

### Fixed
- Restore auto-select of search query input when opening history drawer.

## [v0.20.1] - 2026-10-07

### Maintenance & Chores
- Clean Code modular refactoring and CI two-stage quality gates integration.

## [v0.20.0] - 2026-10-07

### Added
- Padovan sequence path color stripe coding along tab headers for deterministic directory depth identification (`REQ-TAB-025`).

## [v0.19.0] - 2026-10-06

### Added
- auto-select search query on opening history drawer and cancel selection on arrow keys
- sort global search by MRU recency for equal match scores

### Maintenance & Chores
- update _agents submodule with standardized script resolution
