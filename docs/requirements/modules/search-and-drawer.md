# Module Requirements: Search & History Drawer

> **Specification Path**: `docs/requirements/modules/search-and-drawer.md`  
> **Parent Hub**: [`REQUIREMENTS.md`](../../../REQUIREMENTS.md)

This document defines the functional and non-functional requirements specific to the **Search & History Drawer** subsystem of MultiShell. It covers the slide-out history drawer (commands and visited directories), fuzzy search filtering, path-bound history synchronization, and in-terminal text and scrollback search overlay.

---

## Module Overview
- **Module Name**: Search & History Drawer
- **Scope Identifier**: `HIST` / `SEARCH`
- **Architecture Contract**: [`docs/architecture/modules/search-and-drawer.md`](../../architecture/modules/search-and-drawer.md)
- **Primary Domain Specialist**: `PerformanceOptimizer`

---

## Requirements

### `[REQ-TAB-012]` Centered History Overlay via Ctrl+Shift+Middle Click & Shortcuts

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to press `Ctrl+Shift` and click the middle mouse button (scroll wheel click) anywhere within the terminal workspace to open a centered History Overlay showing recent commands and visited directories, closing it with Escape or by clicking outside the dialog.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** the active terminal or terminal workspace, **When** pressing `Ctrl+Shift` and middle-clicking (scroll wheel click), **Then** the interactive History Overlay opens centered over the terminal area.
- [x] **AC-2**: The overlay is centered within the terminal area with a prominent margin to the outer boundary (`Margin="32"`, `MaxWidth="780"`, `MaxHeight="520"`), not full-screen.
- [x] **AC-3**: The overlay displays two tabs: `Commands` and `Directories` with live fuzzy search filtering.
- [x] **AC-4**: Clicking an item in the list sends the command or directory navigation to the active terminal and closes the overlay.
- [x] **AC-5**: Pressing `Escape` or clicking anywhere on the outer semi-transparent backdrop outside the dialog panel closes the overlay and restores terminal focus.
- [x] **AC-6**: Dedicated keyboard shortcuts `Ctrl+Shift+H` (Commands) and `Ctrl+Shift+L` (Directories) continue to open/toggle the centered overlay on the respective tab.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/search-and-drawer.md`

---

### `[REQ-TAB-015]` Tab History Keyboard Navigation & Dedicated Shortcuts (`Ctrl+Shift+H` / `Ctrl+Shift+L`)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** dedicated shortcuts `Ctrl+Shift+H` (open command history) and `Ctrl+Shift+L` (open directory history) with complete keyboard navigation (`Up`/`Down`, `Tab`, `Enter`, `Escape`).

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: `Ctrl+Shift+H` opens or toggles the History Drawer directly on the Commands tab (`Tab 0`).
- [x] **AC-2**: `Ctrl+Shift+L` opens or toggles the History Drawer directly on the Directories tab (`Tab 1`).
- [x] **AC-3**: When opening, the search filter box is focused and the last list item selected by default.
- [x] **AC-4**: `Up` / `Down` navigates entries; `Tab`, `Left`, `Right` switches tabs; `Enter` executes and closes; `Escape` closes and restores terminal focus.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/search-and-drawer.md`

---

### `[REQ-HIST-002]` Live Fuzzy Search & Type-to-Filter in History Drawer

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** real-time fuzzy search filtering in the Commands and Directories drawer so that typing filters and ranks items instantly.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: When typing characters in the drawer, keystrokes are automatically routed to the active search box.
- [x] **AC-2**: The list filters in real time using fuzzy subsequence matching and scoring.
- [x] **AC-3**: Pressing `Enter` executes the top matching item; `Escape` clears the query first or closes the drawer.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/search-and-drawer.md`
- **Test Suite**: `MultiShell.Tests/FuzzySearchServiceTests.cs`

---

### `[REQ-HIST-003]` Path-Based Dynamic Command History, Live Multi-Tab Sync & Exit Pruning

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** multi-tab terminal user,  
> **I want** command history to be bound to the current directory path rather than isolated per tab instance, so that tabs sharing the same path dynamically synchronize commands in real-time, capped at 100 entries per path, and obsolete histories for deleted paths are pruned on exit.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Commands executed at path `P` are recorded in path history for `P`, with duplicates moved to newest position (MRU), and internal setup commands ignored.
- [x] **AC-2**: When path history exceeds 100 entries, oldest items are pruned (FIFO limit 100).
- [x] **AC-3**: Multiple open tabs in path `P` dynamically and immediately reflect new commands in real time.
- [x] **AC-4**: Switching directory from path `A` to `B` dynamically switches the tab's command history to path `B`.
- [x] **AC-5**: On application exit, paths that no longer exist on disk (`Directory.Exists(path) == false`) are pruned before saving state.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/search-and-drawer.md`
- **Test Suite**: `MultiShell.Tests/PathCommandHistoryServiceTests.cs`

---

### `[REQ-HIST-004]` Shared Global Directory History across Tabs with MRU Deduplication & Cap 100

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** multi-tab terminal user,  
> **I want** all terminal tabs to share a single unified directory history in the History drawer, capped at 100 entries with FIFO pruning and MRU deduplication, so that visited paths from any tab are instantly available everywhere.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: When a directory is visited in any tab, it is recorded in the shared `DirectoryHistoryService`.
- [x] **AC-2**: Existing paths are moved to the newest position without duplication (MRU order).
- [x] **AC-3**: Total directory count is capped at 100 entries (FIFO eviction).
- [x] **AC-4**: All open tabs immediately reflect directory changes.
- [x] **AC-5**: Shared directory history is persisted in `WorkspaceState.SharedDirectoryHistory` in `tabs_state.json`.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/search-and-drawer.md`
- **Test Suite**: `MultiShell.Tests/DirectoryHistoryServiceTests.cs`

---

### `[REQ-TERM-006]` In-Terminal Text & Scrollback Search Overlay (`Ctrl+Shift+F`)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** an in-terminal search bar overlay accessible via `Ctrl+Shift+F` so that I can search, highlight, and navigate through text across the active terminal scrollback and screen buffer.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Pressing `Ctrl+Shift+F` displays the search overlay in the top-right corner of the active terminal and focuses the search box.
- [x] **AC-2**: Entering a search query searches across the terminal scrollback and screen buffer in real-time, displaying total matches and current index (e.g. `1/14` or `0/0`).
- [x] **AC-3**: `Enter` or `↓` navigates to next match; `Shift+Enter` or `↑` navigates to previous match; `F3` / `Shift+F3` function keys also navigate matches.
- [x] **AC-4**: Toggling `Match Case` (`Aa`) and `Regular Expression` (`.*`) updates search evaluation accordingly.
- [x] **AC-5**: Pressing `Escape` closes the search overlay and immediately restores keyboard focus to the active terminal.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/search-and-drawer.md`
- **Test Suite**: `MultiShell.Tests/TerminalSearchTests.cs`

---

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-TAB-012` | Centered History Overlay via Ctrl+Shift+Middle Click & Shortcuts | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-015` | Tab History Keyboard Navigation (`Ctrl+Shift+H` / `L`) | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-HIST-002` | Live Fuzzy Search & Type-to-Filter in History Drawer | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-HIST-003` | Path-Based Dynamic Command History, Sync & Pruning | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-HIST-004` | Shared Global Directory History with MRU Cap 100 | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-006` | In-Terminal Text & Scrollback Search (`Ctrl+Shift+F`) | Functional | IMPLEMENTED | `v0.1.0` |
