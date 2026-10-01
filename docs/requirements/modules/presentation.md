# Module Requirements: Presentation & User Interface

> **Specification Path**: `docs/requirements/modules/presentation.md`  
> **Parent Hub**: [`REQUIREMENTS.md`](../../../REQUIREMENTS.md)

This document defines the functional and non-functional requirements specific to the **Presentation, User Interface, and Window Interaction** subsystem of MultiShell. It covers the tab strip, drag-and-drop mechanics, tab switching overlays, keyboard navigation, dialogs, empty-state dashboard, and future split panes.

---

## Module Overview
- **Module Name**: Presentation & User Interface
- **Scope Identifier**: `UI` / `TAB`
- **Architecture Contract**: [`docs/architecture/modules/presentation.md`](../../architecture/modules/presentation.md)
- **Primary Domain Specialist**: `UIDesigner`

---

## Requirements

### `[REQ-TAB-001]` Tabbed User Interface & Dynamic Tab Creation

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to see an intuitive tab bar at the top of the window and open new tabs via an Add Tab button (`+`) or `Ctrl+Shift+T`, each running an isolated shell instance with its own output view.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** the application is running, **Then** a tab bar is rendered at the top of the window displaying all active tabs.
- [x] **AC-2**: **When** the user clicks the `+` button in the tab bar or presses `Ctrl+Shift+T`, **Then** a new tab is created with a unique title (e.g. `PowerShell 1`, `PowerShell 2`).
- [x] **AC-3**: **Then** the newly created tab becomes the active/selected tab immediately.
- [x] **AC-4**: **When** switching between tabs, **Then** the terminal view switches instantly without losing running process state, scrollback, or output.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/MainViewModelTests.cs`

---

### `[REQ-TAB-010]` Tab Keyboard Shortcuts (`Ctrl+Shift+T` / `Ctrl+Shift+D`)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to duplicate the active tab with identical working directory using `Ctrl+Shift+D` and open new tabs via `Ctrl+Shift+T`.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active tab with working directory `D:\project`, **When** the user presses `Ctrl+Shift+D`, **Then** a new tab is created in `D:\project` and placed directly to the right of the active tab.
- [x] **AC-2**: **When** pressing `Ctrl+Shift+T`, **Then** a new tab is created using the default profile and selected.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/MainViewModelTests.cs`

---

### `[REQ-TAB-011]` Tab Drag & Drop Reordering

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to reorder tabs by clicking and dragging a tab header horizontally to a new position in the tab strip.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** multiple open tabs, **When** dragging a tab header past another tab, **Then** the tabs swap positions in real time.
- [x] **AC-2**: **When** releasing the mouse button, **Then** the new order is finalized and preserved.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-TAB-014]` Tab Bar Overflow Visualization & Quick Tab Navigation

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user with many tabs open,  
> **I want** subtle gradient fade indicators and scroll arrow buttons when tabs overflow the window width, along with a quick tab menu button (`≡ ▾`) to see and jump to any tab.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** tabs exceeding visible width, **Then** left and right edge gradient indicators and navigation buttons (`‹` / `›`) appear.
- [x] **AC-2**: **When** clicking `‹` or `›`, **Then** the tab bar scrolls horizontally to reveal clipped tabs.
- [x] **AC-3**: **When** selecting a tab outside the viewport, **Then** it automatically scrolls into full view.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-TAB-016]` Tab Navigation & Cycling via Mouse Wheel over Tab Bar

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to cycle through open tabs by rotating the mouse wheel over the tab bar area.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** multiple open tabs, **When** the user rotates the mouse wheel upwards over the tab bar, **Then** the previous tab is selected.
- [x] **AC-2**: **When** rotating downwards, **Then** the next tab is selected.
- [x] **AC-3**: **Then** the tab bar automatically scrolls to keep the selected tab in view.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-TAB-018]` Comprehensive Tab Keyboard Navigation & Reordering Shortcuts

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** keyboard shortcuts to cycle tabs (`Ctrl+Tab`, `Ctrl+Shift+Tab`, `Ctrl+PageDown`, `Ctrl+PageUp`), jump to numbered tabs (`Ctrl+1`..`8`, `Ctrl+9`), close active tab (`Ctrl+Shift+W` / `Ctrl+Shift+F4`), and move tabs left/right (`Ctrl+Shift+PageUp`/`PageDown`).

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: `Ctrl+Tab` / `Ctrl+PageDown` selects the next tab (cyclic wrap-around).
- [x] **AC-2**: `Ctrl+Shift+Tab` / `Ctrl+PageUp` selects the previous tab (cyclic wrap-around).
- [x] **AC-3**: `Ctrl+1`..`8` jumps to the 1st through 8th tab; `Ctrl+9` jumps to the last tab.
- [x] **AC-4**: `Ctrl+Shift+W` or `Ctrl+Shift+F4` closes the active tab.
- [x] **AC-5**: `Ctrl+Shift+PageUp` / `Ctrl+Shift+PageDown` moves the tab left / right.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/MainViewModelTests.cs`

---

### `[REQ-TAB-019]` Unified Interactive Tab Switcher Overlay (`Ctrl+Tab` & Tab Bar Menu Button)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** a unified Tab Switcher HUD overlay triggered via keyboard (`Ctrl+Tab`) or tab menu button (`≡ ▾`) to preview and switch between all open terminal tabs.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **When** pressing `Ctrl+Tab`, **Then** a centered floating Quick Tab Switcher HUD appears showing open tabs with shell badges and directories.
- [x] **AC-2**: **When** repeatedly pressing `Tab` while holding `Ctrl`, **Then** selection cycles forward; with `Shift`, it cycles backward.
- [x] **AC-3**: **When** releasing `Ctrl`, **Then** the selected tab is activated and the terminal receives focus.
- [x] **AC-4**: **When** pressing `Escape`, **Then** the overlay closes without changing the active tab.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/TabSwitcherTests.cs`

---

### `[REQ-TAB-020]` Custom Tab Renaming & Tab Color Palette Tagging

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to rename terminal tabs with custom titles (via double-click or context menu) and assign colored accent tags so that I can easily identify and organize parallel workspaces.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an open tab, **When** double-clicking the tab header title or selecting `Rename Tab` (`Ctrl+Shift+F2`), **Then** an inline edit box opens to edit the custom title.
- [x] **AC-2**: **When** confirming the new name (pressing `Enter` or clicking outside), **Then** the tab displays the custom title.
- [x] **AC-3**: **When** the custom title is cleared or reset, **Then** the tab falls back to dynamic directory/process naming.
- [x] **AC-4**: **When** right-clicking a tab and selecting a color from the 9-color palette submenu, **Then** an accent color indicator bar is rendered on the tab header.
- [x] **AC-5**: **When** MultiShell restarts, **Then** custom tab titles and assigned colors are persisted and restored from `tabs_state.json`.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/TabRenamingAndColorTests.cs`

---

### `[REQ-TAB-022]` Dynamic Tab Creation via Double-Click on Empty Tab Bar Area

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to double-click in the empty area of the tab bar to quickly open a new terminal tab.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** MultiShell running with tabs, **When** double-clicking in the empty space of the tab bar, **Then** a new tab is created and selected.
- [x] **AC-2**: **When** double-clicking directly on interactive controls (tab button, scroll buttons), **Then** empty-space tab creation does not fire.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-TAB-023]` Tab Switcher Direct Tab Closure via Hover Close Button

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user with multiple tabs open in the Tab Switcher overlay,  
> **I want** to hover over any tab entry to see an "✕" close button at the right edge and click it to directly close that tab while keeping the Tab Switcher overlay open.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **When** hovering over a row in the switcher list, **Then** an "✕" close button becomes smoothly visible (`Opacity = 0.6`, `1.0` on direct hover).
- [x] **AC-2**: **When** clicking "✕", **Then** the tab is closed and added to `ClosedTabs`, and the switcher remains open with updated count.
- [x] **AC-3**: **When** the last remaining tab in the switcher is closed, **Then** the switcher closes and the empty dashboard displays.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-TAB-024]` File & Folder Drag-and-Drop Navigation, Shift-Tab Creation & Tab Bar Drag-Over Activation

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to drag files or folders onto the terminal surface to navigate the active shell, hold `Shift` while dropping to create a new tab in that directory, and have tabs in the tab bar activate automatically when dragging over them.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Dropping a directory onto the terminal surface navigates the active terminal to that directory (`cd`).
- [x] **AC-2**: Dropping a file onto the terminal navigates to its parent directory.
- [x] **AC-3**: Dropping with `Shift` held creates a new tab initialized to that folder.
- [x] **AC-4**: Dragging over a tab header immediately activates that tab.
- [x] **AC-5**: Hovering over overflow scroll arrows (`‹` / `›`) during active drag auto-scrolls the tab bar sequentially (250ms dwell, then 320ms per step).

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/DragDropDirectoryResolverTests.cs`

---

### `[REQ-TAB-025]` Tab Path Color Stripes Coding (All Folders & Deterministic Padovan Width)

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user working across multiple repositories or sibling directories,  
> **I want** each folder level in the working directory path to receive a deterministic color segment arranged horizontally across a fixed-height top bar on the tab, displaying all folders statically without dynamic cross-tab omission or jumping,  
> **so that** related tabs are instantly recognized by their characteristic color pattern while the tab display remains calm and stable.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Each folder segment in a tab's working directory receives a deterministic, aesthetically pleasing color based on case-insensitive FNV-1a hashing and HSL color distribution.
- [x] **AC-2**: Color segments have a fixed base size (`BaseWidth = 9px`, `Height = 3px`), growing horizontally side-by-side from left to right.
- [x] **AC-3**: Width of each folder block scales with strictly increasing Padovan sequence multipliers (`[1, 2, 3, 4, 5, 7, 9, 12, 16, 21, 28, 37]`), giving higher directory levels smaller blocks and deeper levels wider blocks.
- [x] **AC-4**: All folder levels of the directory path are displayed statically; no dynamic cross-tab omission occurs, preventing jumping or shifting of stripes when other tabs are opened, closed, or navigated.
- [x] **AC-5**: Drive root indicators (e.g. `C:`) are omitted so that the color stripes focus on actual directories.
- [x] **AC-6**: When tabs reside in subfolders of a shared structure (e.g. `C:\dt\project\Services` and `C:\dt\project\Core`), they naturally share identical color blocks from left to right for shared ancestors (`dt`, `project`).
- [x] **AC-7**: The width of the tab is strictly determined by its content (icon, title, close button) and never expanded by the color bar. When the unscaled length of the color blocks reaches or exceeds the tab's available width, the entire bar scales down proportionally preserving the relative widths of all blocks (`Viewbox Stretch="Fill" StretchDirection="DownOnly"` constrained by `MaxWidth="{Binding #TabContentPanel.Bounds.Width}"`).

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/PathColorCodingServiceTests.cs`, `MultiShell.Tests/TabPathColorCodingIntegrationTests.cs`

---

### `[REQ-UI-001]` Modern UI Theme, Header Toolbar & Visual Polish

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** a sleek modern dark interface with a top toolbar featuring brand identity, smooth tabs, and quick action buttons.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Top toolbar displays brand logo, title, tab bar, and quick-action buttons (`📜 History`, `⚙ Settings`).

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-UI-002]` Interactive Help & Keyboard Shortcuts Guide

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** an interactive Help modal (accessible via `Ctrl+Shift+F1` or settings menu) showing all keyboard shortcuts and feature explanations.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Pressing `Ctrl+Shift+F1` opens a modal dialog displaying shortcuts and guides.
- [x] **AC-2**: Pressing `Escape` or clicking `✕` closes the modal.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-UI-003]` About Dialog & Technology Information

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** an About dialog displaying version information, architecture overview, and technology stack.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Dialog displays semantic version, tech stack (.NET 10, Avalonia 11, ConPTY), and third-party notices.
- [x] **AC-2**: Pressing `Escape` or clicking `✕` closes the modal.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-UI-004]` 5-Level Font Size Settings for App and Terminal

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to adjust the font size of both the application UI and the terminal independently across 5 distinct levels, with persistent storage across sessions.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: 5 selectable levels (1 to 5) for App Font Size (0.85x to 1.25x) and Terminal Font Size (9.5pt to 16.5pt).
- [x] **AC-2**: Preferences are loaded and restored from `WorkspaceState`.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`
- **Test Suite**: `MultiShell.Tests/FontSizeServiceTests.cs`

---

### `[REQ-UI-005]` Zoom & Font-Size Keyboard & Mouse Wheel Shortcuts

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** quick zoom shortcuts (`Ctrl++`, `Ctrl+-`, `Ctrl+0`, `Ctrl+MouseWheel`) to dynamically adjust font sizes.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: `Ctrl++` / `Ctrl+NumpadPlus` increments font size level.
- [x] **AC-2**: `Ctrl+-` / `Ctrl+NumpadMinus` decrements font size level.
- [x] **AC-3**: `Ctrl+0` / `Ctrl+Numpad0` resets to standard default Level 3.
- [x] **AC-4**: `Ctrl+MouseWheel` over terminal zooms font size.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-UI-008]` Empty State Welcome & Terminal Profile Selector Dashboard

- **Status**: `IMPLEMENTED`
- **Type**: `UI/UX`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user, when all terminal tabs are closed,  
> **I want** an attractive welcome dashboard with instructions and interactive quick-launch cards for all available terminal profiles.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: When `Tabs.Count == 0`, the empty state dashboard displays logo, title, and quick-launch profile cards.
- [x] **AC-2**: Clicking a profile card or pressing `Ctrl+T` opens a new tab and dismisses the dashboard.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-UI-006]` Split Panes (Horizontal & Vertical Session Splits within Tab)

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.1.0`

#### User Story
> **As a** power user,  
> **I want** to split an active tab into horizontal or vertical panes (`Alt+Shift++` / `Alt+Shift+-`), navigating between them with `Alt+ArrowKeys` and resizing separators with the mouse.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: **Given** an active tab, **When** triggering vertical split, **Then** the tab view divides vertically into two side-by-side panes with the same profile and working directory.
- [ ] **AC-2**: **When** triggering horizontal split, **Then** the focused pane divides into two stacked panes.
- [ ] **AC-3**: **When** typing keyboard inputs, **Then** only the currently focused pane receives keystrokes, indicated by an active border highlight.
- [ ] **AC-4**: **When** pressing `Alt+ArrowKeys`, **Then** focus navigates to the adjacent pane in that direction.
- [ ] **AC-5**: **When** a shell process inside a pane exits or the user closes the pane (`Ctrl+Shift+W`), **Then** that pane collapses and remaining panes expand.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-SNIP-001]` Customizable Snippet & Quick Command Launcher

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.2.0`

#### User Story
> **As a** developer,  
> **I want** a quick snippet drawer or overlay with tagged PowerShell scripts and Docker/Git commands that can be inserted or executed with a single click.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: Snippets are categorized, searchable, and insertable into the active terminal with a single click.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-AI-001]` Context-Aware AI Command Generator & Auto-Suggest

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.3.0`

#### User Story
> **As a** terminal user,  
> **I want** an integrated AI assistant overlay (`Ctrl+I` / `Ctrl+K`) that translates natural language requests into contextual PowerShell commands using local LLMs (Ollama) or Cloud APIs (OpenAI/Gemini).

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: Pressing `Ctrl+I` opens a prompt overlay generating commands from natural language input.
- [ ] **AC-2**: `Enter` executes the command; `Tab` pastes into terminal prompt; `Escape` cancels.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/presentation.md`

---

### `[REQ-LNC-001]` Launcher Search & Item Filtering

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.3.0`

#### User Story
> **As a** user,  
> **I want** a quick launcher search box to filter across all available commands, profiles, and history items.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: Fast fuzzy search across commands and configurations with instant preview.

---

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-TAB-001` | Tabbed User Interface & Dynamic Tab Creation | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-010` | Tab Keyboard Shortcuts (`Ctrl+Shift+T` / `Ctrl+Shift+D`) | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-011` | Tab Drag & Drop Reordering | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-014` | Tab Bar Overflow Visualization & Navigation | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-016` | Tab Navigation via Mouse Wheel | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-018` | Tab Keyboard Navigation & Reordering Shortcuts | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-019` | Unified Tab Switcher Overlay (`Ctrl+Tab`) | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-020` | Custom Tab Renaming & Tab Color Palette Tagging | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-022` | Tab Creation via Double-Click on Empty Tab Bar | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-023` | Tab Switcher Direct Tab Closure via Hover Button | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-024` | File & Folder Drag-Drop, Shift-Tab & Tab Drag-Over | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-025` | Tab Path Color Stripes Coding (All Folders & Deterministic Padovan Width) | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-001` | Modern UI Theme, Header Toolbar & Visual Polish | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-002` | Interactive Help & Keyboard Shortcuts Guide (`F1`) | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-003` | About Dialog & Technology Information | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-004` | 5-Level Font Size Settings for App and Terminal | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-005` | Zoom & Font-Size Keyboard & Wheel Shortcuts | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-008` | Empty State Welcome & Profile Selector Dashboard | UI/UX | IMPLEMENTED | `v0.1.0` |
| `REQ-UI-006` | Split Panes (Horizontal & Vertical Session Splits) | Functional | BACKLOG | `v1.1.0` |
| `REQ-SNIP-001` | Customizable Snippet & Quick Command Launcher | Functional | BACKLOG | `v1.2.0` |
| `REQ-AI-001` | Context-Aware AI Command Generator & Auto-Suggest | Functional | BACKLOG | `v1.3.0` |
| `REQ-LNC-001` | Launcher Search & Item Filtering | Functional | BACKLOG | `v1.3.0` |
