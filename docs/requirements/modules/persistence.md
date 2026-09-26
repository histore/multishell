# Module Requirements: Workspace State Persistence

> **Specification Path**: `docs/requirements/modules/persistence.md`  
> **Parent Hub**: [`REQUIREMENTS.md`](../../../REQUIREMENTS.md)

This document defines the functional and non-functional requirements specific to the **Workspace State Persistence** subsystem of MultiShell. It covers serialization and restoration of active tabs, working directories, custom titles, color tags, font sizes, language preferences, path-based histories, and recently closed tabs history.

---

## Module Overview
- **Module Name**: Workspace State Persistence
- **Scope Identifier**: `PERSIST` / `TAB`
- **Architecture Contract**: [`docs/architecture/modules/persistence.md`](../../architecture/modules/persistence.md)
- **Primary Domain Specialist**: `DatabaseSpecialist`

---

## Requirements

### `[REQ-TAB-009]` Tab Session & Working Directory Persistence

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** the application to save all open tabs and their active working directories when changed, and restore them when the application is restarted.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** open tabs in MultiShell, **When** tabs are opened, closed, or their active working directory changes, **Then** state is debounced and serialized to `tabs_state.json`.
- [x] **AC-2**: **When** MultiShell starts, **Then** saved tabs are recreated with their preserved working directories, custom titles, color tags, and selected tab index.
- [x] **AC-3**: **When** the application exits, **Then** `SaveCurrentStateSynchronously()` ensures zero data loss.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/persistence.md`
- **Test Suite**: `MultiShell.Tests/TabStatePersistenceServiceTests.cs`

---

### `[REQ-TAB-013]` Tab Command & Directory History Persistence

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** tab command and directory histories to persist across application restarts so that historical commands and visited folders are restored in the History Drawer.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Command and directory histories are included in `WorkspaceState` serialization in `tabs_state.json`.
- [x] **AC-2**: Upon restart, histories are reloaded and bound to their respective tabs and paths without data loss.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/persistence.md`
- **Test Suite**: `MultiShell.Tests/TabStatePersistenceServiceTests.cs`

---

### `[REQ-TAB-021]` Recently Closed Tabs History & Restoration (Max 10 FIFO)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** a persistent history of my recently closed tabs (capped at 10 items) accessible both from the shell selector dropdown (`▾`) and the empty-state welcome dashboard, so that I can quickly restore closed sessions with their complete command and directory histories or permanently purge them.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: When a tab is closed (via `×`, `Ctrl+W`, or `exit`), its metadata (title, directory, custom title, tab color, history) is pushed to `ClosedTabs`.
- [x] **AC-2**: `ClosedTabs` is capped at a strict FIFO limit of 10 items; older entries are evicted.
- [x] **AC-3**: In the shell selector dropdown or empty dashboard, recently closed tabs are listed with restore and delete options.
- [x] **AC-4**: Clicking a closed tab restores it into a new tab and removes it from `ClosedTabs`.
- [x] **AC-5**: `ClosedTabs` is persisted in `tabs_state.json` across application restarts.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/persistence.md`
- **Test Suite**: `MultiShell.Tests/ClosedTabsHistoryTests.cs`

---

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-TAB-009` | Tab Session & Working Directory Persistence | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-013` | Tab Command & Directory History Persistence | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-021` | Recently Closed Tabs History & Restoration (Max 10 FIFO) | Functional | IMPLEMENTED | `v0.1.0` |
