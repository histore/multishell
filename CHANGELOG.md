# Changelog

All notable changes to this project will be documented in this file.

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
