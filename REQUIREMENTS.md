# Project Requirements & Status Catalog

This document serves as the single source of truth for all functional and non-functional requirements in **MultiShell**.

---

## Requirements Governance Rules
1. **Check Against Existing Requirements**: Every newly requested feature or change must be cross-checked against existing requirements in this file.
2. **Conflict & Duplicate Resolution**: Contradictions, ambiguities, or duplicates must be escalated to the user for explicit decision.
3. **Immutability of Existing Requirements**: Existing requirements may only be modified, replaced, or deleted with explicit user authorization.
4. **100% Coverage**: Every code, architecture, or configuration change must trace back to an approved requirement ID.

---

## Requirements Overview Table

| ID | Title | Category | Status | Verified By |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-LNC-001` | Launcher Search & Item Filtering | Core | **IMPLEMENTED** | `MainViewModelTests` |
| `REQ-TAB-001` | Tabbed User Interface & Dynamic Tab Creation | Terminal | **IMPLEMENTED** | `MainViewModelTabTests` |
| `REQ-TAB-002` | Tab Closure & Process Cleanup | Terminal | **IMPLEMENTED** | `MainViewModelTabTests` |
| `REQ-TAB-003` | Isolated PowerShell (`pwsh.exe`/`powershell.exe`) Execution & Streaming | Terminal | **IMPLEMENTED** | `TerminalTabViewModelTests`, `PowerShellSessionTests` |
| `REQ-TAB-004` | Integrated In-Terminal Prompt & Seamless Canvas | Terminal | **SUPERSEDED by REQ-TAB-007** | `TerminalTabViewModelTests` |
| `REQ-TAB-005` | Direct NuShell TAB Key Evaluation & Auto-Completion | Terminal | **SUPERSEDED by REQ-TAB-007** | `TerminalTabViewModelTests` |
| `REQ-TAB-006` | Authentic Nushell Visual Theme & Reedline Completion UI | Terminal | **SUPERSEDED by REQ-TAB-007** | `TerminalTabViewModelTests` |
| `REQ-TAB-007` | True Terminal Emulation via ConPTY (PowerShell) | Terminal | **IMPLEMENTED** | `PowerShellSessionTests`, `TerminalTabViewModelTests` |
| `REQ-TAB-008` | Working Directory (CWD) Tracking via OSC Escape Sequences | Terminal | **IMPLEMENTED** | `PowerShellSessionTests`, `TerminalTabViewModelTests` |
| `REQ-TAB-009` | Tab Session & Working Directory Persistence | Storage | **IMPLEMENTED** | `TabStatePersistenceServiceTests`, `MainViewModelTabTests` |
| `REQ-TAB-010` | Tab Keyboard Shortcuts (`Ctrl+Shift+T` / `Ctrl+Shift+D`) | Interaction | **IMPLEMENTED** | `MainViewModelTabTests` |
| `REQ-TAB-011` | Tab Drag & Drop Reordering | Interaction | **IMPLEMENTED** | `MainViewModelTabTests` |
| `REQ-TAB-012` | Tab History Hover Overlay (Commands & Directories) | Interaction | **IMPLEMENTED** | `TerminalTabViewModelTests`, `PowerShellSessionTests` |
| `REQ-TAB-013` | Tab Command & Directory History Persistence | Storage | **IMPLEMENTED** | `TabStatePersistenceServiceTests`, `MainViewModelTabTests` |
| `REQ-TAB-014` | Tab Bar Overflow Visualization & Quick Tab Navigation | Interaction | **IMPLEMENTED** | `MainViewModelTabTests`, `MainWindow` |
| `REQ-TAB-015` | Tab History Keyboard Navigation & Toggle Shortcut (`Ctrl+Shift+H`) | Interaction | **IMPLEMENTED** | `MainWindow` |
| `REQ-UI-001` | Modern UI Theme, Header Toolbar & Visual Polish | UI | **IMPLEMENTED** | `MainWindow` |
| `REQ-UI-002` | Interactive Help & Keyboard Shortcuts Guide | UI | **IMPLEMENTED** | `MainWindow` |
| `REQ-UI-003` | About Dialog & Technology Information | UI | **IMPLEMENTED** | `MainWindow` |
| `REQ-GOV-001` | Subagent Roles & Context Isolation | Architecture | **IMPLEMENTED** | `.agents/rules/subagents.md` |
| `REQ-GOV-002` | Dynamic Model & Reasoning Depth Allocation | Architecture | **IMPLEMENTED** | `.agents/skills/subagent-control` |
| `REQ-GOV-003` | Requirements Immutability & Conflict Escalation | Governance | **IMPLEMENTED** | Quality Gate / Verification |
| `REQ-LOC-001` | Dynamic Multi-Language UI (DE, EN, FR, ES) with Persistence | Localization | **IMPLEMENTED** | `LocalizationServiceTests` |
| `REQ-HIST-002` | Live Fuzzy Search & Type-to-Filter in History Drawer | Interaction | **IMPLEMENTED** | `FuzzySearchServiceTests` |

---

## Detailed Specifications

### REQ-LNC-001: Launcher Search & Item Filtering
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want to search and filter application/command launcher items by name or category so that I can quickly find target applications.
- **Acceptance Criteria**:
  - **Given** a list of launcher items,
  - **When** the user enters a search query,
  - **Then** `FilteredItems` contains only items matching the search query (case-insensitive) in `Name` or `Category`.

---

### REQ-TAB-001: Tabbed User Interface & Dynamic Tab Creation
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want a tabbed interface with an Add Tab button (`+ New Tab`) so that I can open multiple parallel terminal tabs.
- **Acceptance Criteria**:
  - **Given** the application is launched,
  - **When** the main window loads without saved state,
  - **Then** an initial tab (`PS 1`) is created and selected by default.
  - **When** the user clicks `+ New Tab`,
  - **Then** a new numbered tab (`PS 2`, `PS 3`, ...) is added and immediately selected.

---

### REQ-TAB-002: Tab Closure & Bidirectional Process Lifecycle
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want tabs and their underlying shell processes to be bidirectionally tied together: when the shell process ends, the tab closes automatically; when a tab is closed, the shell process is terminated; and if termination is not possible, the tab remains open.
- **Acceptance Criteria**:
  - **Given** an active tab with a running shell process,
  - **When** the shell process terminates (e.g. user enters `exit` or process ends),
  - **Then** the tab is automatically closed and removed from the tab bar.
  - **When** the user clicks the `✕` close button on a tab header,
  - **Then** the application attempts to terminate the underlying shell process,
  - **And** if process termination succeeds, the tab is removed from the tab bar,
  - **And** if the closed tab was selected, an adjacent tab is selected,
  - **And** if process termination fails or is not possible, the tab remains open.

---

### REQ-TAB-003: Isolated PowerShell (`pwsh.exe`/`powershell.exe`) Execution & Streaming
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want each tab to run an isolated PowerShell instance (`pwsh.exe` with fallback to `powershell.exe`) with live terminal output and input streaming.
- **Acceptance Criteria**:
  - **Given** an active tab,
  - **When** commands are entered,
  - **Then** the command is piped to standard input of the dedicated PowerShell process via ConPTY,
  - **And** output and ANSI sequences are streamed in real time to the terminal display.

---

### REQ-TAB-007: True Terminal Emulation via ConPTY (PowerShell)
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want each terminal tab to behave like a real terminal window so that PowerShell runs as an authentic interactive REPL with PSReadLine, syntax highlighting, TAB completion, and history navigation.
- **Supersedes**: `REQ-TAB-004`, `REQ-TAB-005`, `REQ-TAB-006`.
- **Acceptance Criteria**:
  - **Given** a terminal tab is opened,
  - **When** the tab loads,
  - **Then** PowerShell starts as an interactive REPL process via Windows ConPTY,
  - **And** the terminal renders using `SvcSystems.UI.Terminal`,
  - **And** all keyboard input is passed directly to the PTY,
  - **And** all terminal output (including ANSI escape sequences, colors, cursor positioning) is rendered natively.
  - **When** the terminal window is resized,
  - **Then** the PTY dimensions are updated and the shell reflows accordingly.
  - **When** the tab is closed,
  - **Then** the ConPTY session and PowerShell process are terminated cleanly.

---

### REQ-TAB-008: Working Directory (CWD) Tracking & Path Tab Titles
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want the application to track the active working directory of each terminal tab in real time so that tab headers display the current path with prefix ellipsis (`...`) when space is limited and the state can be saved.
- **Acceptance Criteria**:
  - **Given** a running PowerShell terminal tab,
  - **When** the directory changes (e.g. via `cd` or `Set-Location`),
  - **Then** the shell emits an OSC 9;9 or OSC 7 escape sequence with the new path,
  - **And** the terminal session intercepts the sequence and updates its `WorkingDirectory` property and triggers `WorkingDirectoryChanged`,
  - **And** the tab viewmodel updates its `Title` to the full path,
  - **And** the tab header renders the title with `TextTrimming="PrefixCharacterEllipsis"`, ensuring the last path component remains visible when space is constrained.

---

### REQ-TAB-009: Tab Session & Working Directory Persistence
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want the application to save all open tabs and their active working directories when changed, and restore them when the application is restarted.
- **Acceptance Criteria**:
  - **Given** open tabs with specific working directories in MultiShell,
  - **When** tabs are opened, closed, or their active working directory changes,
  - **Then** the state (open tab list, titles, directories, selected tab index) is persisted to local storage (`%LOCALAPPDATA%/MultiShell/tabs_state.json`),
  - **When** the application starts up,
  - **Then** previously saved tabs are restored at their saved working directories.

---

### REQ-TAB-010: Tab Keyboard Shortcuts (`Ctrl+Shift+T` / `Ctrl+Shift+D`)
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want standard keyboard shortcuts to create a new tab (`Ctrl+Shift+T`) and duplicate the active tab (`Ctrl+Shift+D` with same working directory) without conflicting with standard terminal escape sequences.
- **Acceptance Criteria**:
  - **Given** MultiShell is active,
  - **When** pressing `Ctrl+Shift+T`,
  - **Then** a new tab is created and selected in the default directory.
  - **When** pressing `Ctrl+Shift+D`,
  - **Then** a new tab is created and selected with the current working directory of the active tab.

---

### REQ-TAB-011: Tab Drag & Drop Reordering
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want to reorder terminal tabs by dragging and dropping tab headers so that I can organize parallel tabs visually according to my workflow.
- **Acceptance Criteria**:
  - **Given** multiple open terminal tabs in MultiShell,
  - **When** the user drags a tab header and drops it over another tab position,
  - **Then** the tab is moved to the target index in the `Tabs` collection,
  - **And** the moved tab remains selected/active,
  - **And** the updated order is persisted to storage.

---

### REQ-TAB-012: Tab History Hover Overlay (Commands & Directories)
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want a left-side hover area that reveals an overlay displaying the executed command history and visited directory history of the active tab, allowing me to view and re-execute previous commands or navigate to previous folders.
- **Acceptance Criteria**:
  - **Given** an active PowerShell terminal tab in MultiShell,
  - **When** commands are executed in the tab,
  - **Then** each executed command is tracked into `CommandHistory` and directory changes into `DirectoryHistory`.
  - **When** hovering over the left edge of the window,
  - **Then** a flyout overlay displays the command history and directory history of the active tab.
  - **When** clicking a history entry,
  - **Then** the command or folder navigation is sent directly to the active terminal.
  - **When** moving the pointer out of the overlay,
  - **Then** the overlay closes smoothly.

---

### REQ-TAB-013: Tab Command & Directory History Persistence
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want the command history and visited directory history of each tab to be persisted across application restarts and restored when reopening the application, and removed from storage when a tab is closed.
- **Acceptance Criteria**:
  - **Given** open terminal tabs with accumulated command and directory histories,
  - **When** commands are executed or working directories change,
  - **Then** `CommandHistory` and `DirectoryHistory` are persisted to local storage (`tabs_state.json`).
  - **When** the application starts up,
  - **Then** previously saved command and directory histories are restored for each persisted tab.
  - **When** a tab is closed (by user action or shell exit),
  - **Then** the tab is removed and its persisted state and histories are purged from storage.

---

### REQ-TAB-014: Tab Bar Overflow Visualization & Quick Tab Navigation
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want visual indicators when there are more tabs than can be displayed in the visible tab bar, along with scroll buttons and a quick tab dropdown menu, so that I can easily navigate and switch between all open tabs.
- **Acceptance Criteria**:
  - **Given** many open tabs exceeding the visible horizontal space of the tab bar,
  - **When** overflow occurs,
  - **Then** visual edge gradient indicators and scroll buttons (`◀` and `▶`) appear to indicate overflow to the left and right.
  - **When** the user clicks `◀` or `▶`,
  - **Then** the tab bar scrolls smoothly in the respective direction.
  - **When** the user clicks the tab list button (`▼`),
  - **Then** a dropdown/flyout opens listing all open tabs with their titles and active status, allowing direct 1-click tab selection.
  - **When** an active tab changes or a new tab is created,
  - **Then** the tab bar automatically scrolls to bring the selected tab into view.

---

### REQ-TAB-015: Tab History Keyboard Navigation & Toggle Shortcut (`Ctrl+Shift+H`)
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want to toggle the history drawer using `Ctrl+Shift+H`, navigate through items with arrow keys with the last item preselected, and execute/apply the selected item by pressing `Enter`.
- **Acceptance Criteria**:
  - **Given** the main application window is active,
  - **When** pressing `Ctrl+Shift+H`,
  - **Then** the history drawer toggles between visible and hidden.
  - **When** the history drawer opens,
  - **Then** the last item in the active history list is selected and focused by default.
  - **When** pressing `Up` or `Down`,
  - **Then** selection moves between history entries.
  - **When** pressing `Left` or `Right`,
  - **Then** the active tab switches between command history and directory history.
  - **When** pressing `Enter`,
  - **Then** the selected command or directory navigation is sent to the terminal and the overlay closes immediately, focusing the terminal.
  - **When** pressing `Escape`,
  - **Then** the overlay closes without execution and focuses the terminal.

---

### REQ-UI-001: Modern UI Theme, Header Toolbar & Visual Polish
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want a sleek modern dark interface with a top toolbar featuring brand identity, smooth tabs, and quick action buttons.
- **Acceptance Criteria**:
  - **Given** the application is running,
  - **Then** the top toolbar displays the MultiShell brand logo, title, tab bar, and quick-action buttons (`📜 History`, `❓ Help`, `ℹ About`).

---

### REQ-UI-002: Interactive Help & Keyboard Shortcuts Guide
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want an interactive Help modal (accessible via `F1` or top toolbar button) showing all keyboard shortcuts and feature explanations.
- **Acceptance Criteria**:
  - **Given** MultiShell is active,
  - **When** pressing `F1` or clicking `❓ Help`,
  - **Then** a modal dialog opens displaying shortcuts (`Ctrl+Shift+T`, `Ctrl+Shift+D`, `Ctrl+Shift+H`, `F1`, `ESC`) and feature guides.
  - **When** pressing `Escape` or clicking `✕` / backdrop,
  - **Then** the modal closes.

---

### REQ-UI-003: About Dialog & Technology Information
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want an About dialog (accessible via `ℹ About` toolbar button) displaying version information, architecture overview, and technology stack.
- **Acceptance Criteria**:
  - **Given** MultiShell is active,
  - **When** clicking `ℹ About`,
  - **Then** a modal dialog opens with version `1.0.0`, tech stack (`.NET 10`, `Avalonia 11`, `ConPTY`), and architecture details.
  - **When** pressing `Escape` or clicking `✕` / backdrop,
  - **Then** the modal closes.

---

### REQ-GOV-001: Subagent Roles & Context Isolation
- **Status**: `IMPLEMENTED`
- **User Story**: As a development orchestrator, I want specialized subagents (`Control`, `RequirementEngineer`, `Troubleshooter`, `UIDesigner`, `LocalizationSpecialist`, `Architekt`, `Developer`, `RefactoringSpecialist`, `PerformanceOptimizer`, `SecurityAuditor`, `DocumentationSpecialist`, `ReleaseManager`, `Tester`, `Verifikation`) operating with minimal isolated contexts adhering to Clean Code, Clean Architecture, and full Internationalization.
- **Acceptance Criteria**:
  - **Given** a new task, optimization, or bug report,
  - **When** subagents are dispatched,
  - **Then** each role receives only the minimal necessary context package without bloated history.

---

### REQ-GOV-002: Dynamic Model & Reasoning Depth Allocation
- **Status**: `IMPLEMENTED`
- **User Story**: As the Control agent, I want to assign appropriate models and reasoning levels (`High` for Requirements/Troubleshooter/UI-Design/Architecture/Performance/Security/Verification, `Medium` for Localization/Developer/Refactoring/Documentation/ReleaseManager/Tester) depending on cognitive requirements.
- **Acceptance Criteria**:
  - **Given** a stage in the execution pipeline,
  - **When** assigning the subagent role,
  - **Then** the model profile and reasoning depth match the role's allocation matrix.

---

### REQ-GOV-003: Requirements Immutability & Conflict Escalation
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want all new requirements verified against existing ones, conflicts escalated for user decision, and existing requirements preserved as immutable unless explicitly instructed.
- **Acceptance Criteria**:
  - **Given** a new user request,
  - **When** duplicates or contradictions are found against `REQUIREMENTS.md`,
  - **Then** the user is prompted to resolve the conflict before any changes are made.

---

### REQ-REL-001: Git-Tag-Based Dynamic Semantic Versioning
- **Status**: `IMPLEMENTED`
- **User Story**: As a user and developer, I want the application version dynamically determined from Git tags (e.g. `v0.0.1`, `v1.0.0`) with a default initial fallback of `0.0.1`, and displayed in the About dialog.
- **Acceptance Criteria**:
  - **Given** the application is compiled,
  - **When** a Git tag `vX.Y.Z` exists (or fallback `0.0.1`),
  - **Then** `MainViewModel.AppVersion` returns the formatted semantic version string `vX.Y.Z`.
  - **When** the user opens the About modal,
  - **Then** the dynamic version is displayed in the Version field.

---

### REQ-REL-002: GitHub Actions CI/CD Pipeline
- **Status**: `IMPLEMENTED`
- **User Story**: As a maintainer, I want standardized GitHub Actions workflows for continuous integration (testing on `main` push/PR) and automated single-file Windows releases on version tags (`v*.*.*`).
- **Acceptance Criteria**:
  - **Given** a pull request or push to `main`,
  - **When** the CI workflow triggers,
  - **Then** `.NET 10` dependencies are restored and all automated tests are executed (`dotnet test`).
  - **Given** a version tag `v*.*.*` is pushed,
  - **When** the Release workflow triggers,
---

### REQ-SET-001: Unified Settings Menu & Dynamic Theme Switching
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want a unified Settings menu (gear icon ⚙) in the top toolbar providing dynamic Dark/Light theme switching, Help & Shortcuts (`F1`), and About dialog access.
- **Acceptance Criteria**:
  - **Given** the application is running,
  - **When** clicking the Settings gear button `⚙`,
  - **Then** a dropdown menu appears with Theme toggle, Help, and About items.
  - **When** clicking the Theme option,
  - **Then** the application dynamically switches between Dark and Light theme variants.
  - **When** clicking Help or About,
  - **Then** the respective modal dialog opens and the menu closes.

---

## 🔮 Future / Backlog Features

### REQ-AI-001: Context-Aware AI Command Generator & Auto-Suggest
- **Status**: `PLANNED`
- **User Story**: As a terminal user, I want an integrated AI assistant overlay (triggered via shortcut `Ctrl+I` / `Ctrl+K`) that translates natural language requests into contextual PowerShell commands using local LLMs (Ollama) or Cloud APIs (OpenAI/Gemini), with one-click execution (`Enter`) and inline pasting (`Tab`).
- **Acceptance Criteria**:
  - **Given** an active PowerShell terminal tab,
  - **When** the user presses `Ctrl+I` or `Ctrl+K`,
  - **Then** a floating AI prompt overlay appears.
  - **When** entering a natural language request,
  - **Then** the AI service (Ollama/OpenAI) generates the exact PowerShell command with safety explanations, taking current working directory and recent history into context.
  - **When** pressing `Enter`,
  - **Then** the command is executed in the active ConPTY session and the overlay closes.
  - **When** pressing `Tab`,
  - **Then** the command is pasted into the active terminal prompt without executing.
  - **When** pressing `Escape`,
  - **Then** the overlay closes without modifications.

---

### REQ-LOC-001: Dynamic Multi-Language UI (DE, EN, FR, ES) with Persistence
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want the UI to automatically adapt to my operating system language (with English fallback) and allow switching between German, English, French, and Spanish with persistent storage across restarts.
- **Acceptance Criteria**:
  - **Given** an OS configured in German, French, Spanish or English, the UI defaults to that language.
  - **When** the user manually chooses a language from the Settings flyout (`[ DE | EN | FR | ES ]`),
  - **Then** all UI elements update immediately in real time, and the preference is persisted in `WorkspaceState.SavedLanguage`.

---

### REQ-HIST-002: Live Fuzzy Search & Type-to-Filter in History Drawer
- **Status**: `IMPLEMENTED`
- **User Story**: As a user, I want a real-time fuzzy search filter in the History (Commands) and Directories drawer so that whenever I start typing, matching items are filtered and ranked instantly.
- **Acceptance Criteria**:
  - **Given** the History Drawer is open (`Ctrl+Shift+H` or left-edge hover),
  - **When** the user types characters on the keyboard that are not navigation commands,
  - **Then** keystrokes are automatically routed to the active search box, and the list filters in real time using fuzzy subsequence matching and scoring.
  - **When** the user presses `Enter`, the top matching command or directory is executed/navigated.
  - **When** the user presses `Escape`, the active filter query is cleared first or the drawer is closed.
