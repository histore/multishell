# MultiShell Requirements Specification (Central Hub)

> **Document Role**: Central Requirements Hub, Global NFR Baseline & Module Registry  
> **Architecture Alignment**: [`ARCHITECTURE.md`](ARCHITECTURE.md)  
> **Governance Standard**: [`AGENTS.md`](AGENTS.md)

This document is the **Single Source of Truth** for all functional, non-functional, and technical requirements of MultiShell. Adhering to the **Modular Hub-and-Spoke Requirements Architecture**, domain-specific requirements are partitioned into dedicated module specifications under `docs/requirements/modules/`, while this root hub establishes global non-functional baselines, cross-cutting constraints, and the central index.

---

## Modular Requirements Architecture

To prevent cognitive overload, maintain strict context hygiene for subagents, and eliminate token bloat, requirements are segmented along the project's modular architecture boundaries:

```
docs/requirements/modules/
├── terminal-session.md          # ConPTY, shell lifecycle, ANSI, UTF-8, ConPTY win32con, hyperlinks
├── presentation.md              # Tab bar, drag & drop, tab switcher, keyboard navigation, modals, split panes
├── search-and-drawer.md         # History drawer, fuzzy search, in-terminal search overlay
├── profiles-and-configuration.md # Shell profiles, working directories, global settings flyout
├── persistence.md               # tabs_state.json, workspace state, closed tabs history
├── theming-and-styling.md       # Dark/Light theme tokens, Xterm color palettes, Windows 11 Mica/Acrylic
└── localization.md              # Dynamic bilingual & 6-language i18n/l10n resources
```

---

## Governance Rules

1. **100% Coverage Mandate**: Every system modification (production code, architecture refactoring, configuration, tests) must map directly to an approved Requirement ID.
2. **Consistency & Cross-Module Deduplication**: New requirements must be audited against existing requirements across all active module specifications.
3. **User Decision on Conflicts**: If a conflict or contradiction arises between specifications, agents MUST pause and escalate the decision directly to the user.
4. **Immutability of Existing Requirements**: Existing requirements may only be modified or deprecated with explicit user instructions.
5. **Namespaced Scoped IDs**: All requirements follow the scoped pattern: `REQ-<SCOPE>-XXX` (e.g. `REQ-TAB-001`, `REQ-TERM-002`, `REQ-UI-003`, `REQ-HIST-004`).
6. **Subagent Context Isolation**: Downstream agents (`Developer`, `Tester`, `Verifikation`) receive only this central index and the specific target module specification file (`docs/requirements/modules/<module>.md`) to maintain strict context hygiene.

---

## Module Registry

| Module / Subsystem | Scope Prefix | Requirements Specification | Architecture Specification | Status |
| :--- | :--- | :--- | :--- | :--- |
| **Terminal Session & ConPTY** | `TERM` / `TAB` | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | [`docs/architecture/modules/terminal-session.md`](docs/architecture/modules/terminal-session.md) | `ACTIVE` |
| **Presentation & User Interface** | `UI` / `TAB` | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | [`docs/architecture/modules/presentation.md`](docs/architecture/modules/presentation.md) | `ACTIVE` |
| **Search & History Drawer** | `HIST` / `SEARCH` | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | [`docs/architecture/modules/search-and-drawer.md`](docs/architecture/modules/search-and-drawer.md) | `ACTIVE` |
| **Profiles & Configuration** | `PROF` / `SET` | [`docs/requirements/modules/profiles-and-configuration.md`](docs/requirements/modules/profiles-and-configuration.md) | [`docs/architecture/modules/profiles-and-configuration.md`](docs/architecture/modules/profiles-and-configuration.md) | `ACTIVE` |
| **Workspace Persistence** | `PERSIST` / `TAB` | [`docs/requirements/modules/persistence.md`](docs/requirements/modules/persistence.md) | [`docs/architecture/modules/persistence.md`](docs/architecture/modules/persistence.md) | `ACTIVE` |
| **Theming & Styling** | `THEME` / `UI` | [`docs/requirements/modules/theming-and-styling.md`](docs/requirements/modules/theming-and-styling.md) | [`docs/architecture/modules/theming-and-styling.md`](docs/architecture/modules/theming-and-styling.md) | `ACTIVE` |
| **Localization & i18n** | `LOC` / `I18N` | [`docs/requirements/modules/localization.md`](docs/requirements/modules/localization.md) | [`docs/architecture/modules/localization.md`](docs/architecture/modules/localization.md) | `ACTIVE` |

---

## Global Non-Functional Requirements (NFR Baseline)

### `[REQ-GOV-001]` Subagent Roles & Context Isolation

- **Status**: `IMPLEMENTED`
- **Type**: `Governance`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** development orchestrator,  
> **I want** specialized subagents operating with minimal isolated contexts adhering to Clean Code, Clean Architecture, and full Internationalization, maintained declaratively in a Single Source of Truth (`_agents/rules/model-tiers.json`).

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** a new task, optimization, or bug report, **When** subagents are dispatched, **Then** each role receives only the minimal necessary context package without bloated history.
- [x] **AC-2**: All 22 subagent roles are defined declaratively in `_agents/rules/model-tiers.json`.

---

### `[REQ-GOV-002]` Dynamic Model & Reasoning Depth Allocation

- **Status**: `IMPLEMENTED`
- **Type**: `Governance`
- **Target Release**: `v0.1.0`

#### User Story
> **As the** Control agent,  
> **I want** to assign appropriate models and reasoning levels (Tiers 1 to 4 with High, Medium, Low/Fast thinking budgets) defined dynamically via the Single Source of Truth (`_agents/rules/model-tiers.json`).

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Subagent roles are matched to cognitive tiers and thinking budgets based on task complexity.

---

### `[REQ-GOV-003]` Requirements Immutability & Conflict Escalation

- **Status**: `IMPLEMENTED`
- **Type**: `Governance`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** all new requirements verified against existing ones, conflicts escalated for user decision, and existing requirements preserved as immutable unless explicitly instructed.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Any requirement contradiction or duplicate intent must be paused and presented to the user for explicit decision.

---

### `[REQ-REL-001]` Git-Tag-Based Dynamic Semantic Versioning & Main-Branch Enforcement

- **Status**: `IMPLEMENTED`
- **Type**: `Release / CI`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** maintainer and release manager,  
> **I want** release versions dynamically determined from Git tags (`vX.Y.Z` via MinVer) with release tagging strictly restricted to the `main` branch.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Application version dynamically compiles from Git tags, falling back to `0.0.1` in development builds.
- [x] **AC-2**: Release tags and GitHub Release builds are strictly enforced on `main`.

---

### `[REQ-REL-002]` GitHub Actions CI/CD Pipeline

- **Status**: `IMPLEMENTED`
- **Type**: `Infrastructure`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** maintainer,  
> **I want** standardized GitHub Actions workflows for continuous integration (testing on `main` push/PR) and automated single-file Windows releases on version tags (`v*.*.*`).

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: PR and push builds compile and test on Windows runner with zero warnings and 100% test pass rate.
- [x] **AC-2**: Releases generate portable ZIP and Inno Setup installers automatically on tag push.

---

### `[REQ-REL-003]` Multi-Target Release Packaging (Self-Contained & Framework-Dependent)

- **Status**: `IMPLEMENTED`
- **Type**: `Infrastructure`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user with an existing .NET 10 desktop runtime,  
> **I want** an optional lightweight, framework-dependent release archive alongside the standard self-contained installer and portable builds.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Release workflow produces both self-contained (portable + installer) and lightweight framework-dependent binaries (< 20 MB).

---

### `[REQ-SEC-001]` Automated Secret Scanning & Dependency Vulnerability Auditing

- **Status**: `IMPLEMENTED`
- **Type**: `Security`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** security officer,  
> **I want** automated secret scanning (Gitleaks) and NuGet package vulnerability audits to run on every commit and PR.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Gitleaks and `dotnet list package --vulnerable` run as required CI checks on every push and PR.

---

### `[REQ-LEGAL-001]` Third-Party License Notices & Font Attribution

- **Status**: `IMPLEMENTED`
- **Type**: `Compliance`
- **Target Release**: `v0.1.0`

#### User Story
> **As an** open-source user and maintainer,  
> **I want** comprehensive third-party software and font license notices in `THIRD_PARTY_NOTICES.md` and referenced in the About dialog.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: `THIRD_PARTY_NOTICES.md` attributes all libraries and the embedded FiraCode Nerd Font Mono (OFL-1.1).

---

## Master Requirements Traceability Matrix

| Requirement ID | Title | Target Module Specification | Status |
| :--- | :--- | :--- | :--- |
| `REQ-TAB-001` | Tabbed User Interface & Dynamic Tab Creation | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-002` | Tab Closure & Bidirectional Process Lifecycle | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TAB-003` | Isolated PowerShell Execution & Streaming | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TAB-007` | True Terminal Emulation via ConPTY (PowerShell) | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TAB-008` | Working Directory (CWD) Tracking & Path Tab Titles | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TAB-009` | Tab Session & Working Directory Persistence | [`docs/requirements/modules/persistence.md`](docs/requirements/modules/persistence.md) | `IMPLEMENTED` |
| `REQ-TAB-010` | Tab Keyboard Shortcuts (`Ctrl+Shift+T` / `D`) | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-011` | Tab Drag & Drop Reordering | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-012` | Centered History Overlay via Ctrl+Shift+Middle Click & Shortcuts | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | `IMPLEMENTED` |
| `REQ-TAB-013` | Tab Command & Directory History Persistence | [`docs/requirements/modules/persistence.md`](docs/requirements/modules/persistence.md) | `IMPLEMENTED` |
| `REQ-TAB-014` | Tab Bar Overflow Visualization & Navigation | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-015` | Tab History Keyboard Navigation (`Ctrl+Shift+H`/`L`) | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | `IMPLEMENTED` |
| `REQ-TAB-016` | Tab Navigation & Cycling via Mouse Wheel | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-017` | Terminal Text Selection, Copy & Paste | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TAB-018` | Comprehensive Tab Keyboard Navigation Shortcuts | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-019` | Unified Tab Switcher Overlay (`Ctrl+Tab`) | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-020` | Custom Tab Renaming & Tab Color Palette Tagging | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-021` | Recently Closed Tabs History & Restoration (Max 10) | [`docs/requirements/modules/persistence.md`](docs/requirements/modules/persistence.md) | `IMPLEMENTED` |
| `REQ-TAB-022` | Tab Creation via Double-Click on Empty Tab Bar | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-023` | Tab Switcher Direct Tab Closure via Hover Button | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TAB-024` | File & Folder Drag-Drop, Shift-Tab & Tab Drag-Over | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-TERM-001` | Robust UTF-8 Character Streaming & Monospace Glyphs | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-002` | Multi-line Newline Insertion via Ctrl/Shift+Enter | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-003` | Terminal Scrollback & Buffer Control Shortcuts | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-004` | Multi-Chunk ANSI/VT100 Sequence Preservation | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-005` | Clickable Hyperlinks & Local File Paths via Ctrl+Click | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-006` | In-Terminal Text & Scrollback Search (`Ctrl+Shift+F`) | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | `IMPLEMENTED` |
| `REQ-TERM-007` | Broadcast / Multi-Input Mode across Tabs / Panes | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `BACKLOG` |
| `REQ-TERM-009` | Embedded Monospace Nerd Font (FiraCode Nerd Font Mono)| [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-010` | Native Windows ConPTY Environment & OSC 11 | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-TERM-011` | Smooth Terminal Rendering & Overlay Scrollbar Anti-Flicker| [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md)| `IMPLEMENTED` |
| `REQ-HIST-002` | Live Fuzzy Search & Type-to-Filter in History Drawer | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | `IMPLEMENTED` |
| `REQ-HIST-003` | Path-Based Dynamic Command History, Sync & Pruning | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | `IMPLEMENTED` |
| `REQ-HIST-004` | Shared Global Directory History across Tabs (MRU 100) | [`docs/requirements/modules/search-and-drawer.md`](docs/requirements/modules/search-and-drawer.md) | `IMPLEMENTED` |
| `REQ-CLI-001` | Startup Arguments & Single-Instance Tab Activation | [`docs/requirements/modules/terminal-session.md`](docs/requirements/modules/terminal-session.md) | `IMPLEMENTED` |
| `REQ-UI-001` | Modern UI Theme, Header Toolbar & Visual Polish | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-UI-002` | Interactive Help & Keyboard Shortcuts Guide (`F1`) | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-UI-003` | About Dialog & Technology Information | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-UI-004` | 5-Level Font Size Settings for App and Terminal | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-UI-005` | Zoom & Font-Size Keyboard & Wheel Shortcuts | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-UI-006` | Split Panes (Horizontal & Vertical Session Splits) | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `BACKLOG` |
| `REQ-UI-007` | Windows 11 Acrylic & Mica Window Backdrop Effects | [`docs/requirements/modules/theming-and-styling.md`](docs/requirements/modules/theming-and-styling.md) | `BACKLOG` |
| `REQ-UI-008` | Empty State Welcome & Profile Selector Dashboard | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `IMPLEMENTED` |
| `REQ-SET-001` | Unified Settings Menu & Dynamic Theme Switching | [`docs/requirements/modules/profiles-and-configuration.md`](docs/requirements/modules/profiles-and-configuration.md) | `IMPLEMENTED` |
| `REQ-LOC-001` | Dynamic Multi-Language UI (DE, EN, FR, ES, IT, PT) | [`docs/requirements/modules/localization.md`](docs/requirements/modules/localization.md) | `IMPLEMENTED` |
| `REQ-PROF-001` | Configurable Startup Working Directory per Profile | [`docs/requirements/modules/profiles-and-configuration.md`](docs/requirements/modules/profiles-and-configuration.md) | `IMPLEMENTED` |
| `REQ-SNIP-001` | Customizable Snippet & Quick Command Launcher | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `BACKLOG` |
| `REQ-AI-001` | Context-Aware AI Command Generator & Auto-Suggest | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `BACKLOG` |
| `REQ-LNC-001` | Launcher Search & Item Filtering | [`docs/requirements/modules/presentation.md`](docs/requirements/modules/presentation.md) | `BACKLOG` |
| `REQ-GOV-001` | Subagent Roles & Context Isolation | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-GOV-002` | Dynamic Model & Reasoning Depth Allocation | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-GOV-003` | Requirements Immutability & Conflict Escalation | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-REL-001` | Git-Tag-Based Semantic Versioning & Main Release | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-REL-002` | GitHub Actions CI/CD Pipeline | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-REL-003` | Multi-Target Release Packaging (Self-Contained & Light) | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-SEC-001` | Automated Secret Scanning & Vulnerability Auditing | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
| `REQ-LEGAL-001`| Third-Party License Notices & Font Attribution | [`REQUIREMENTS.md`](REQUIREMENTS.md) | `IMPLEMENTED` |
