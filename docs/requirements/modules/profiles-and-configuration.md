# Module Requirements: Profiles & Configuration

> **Specification Path**: `docs/requirements/modules/profiles-and-configuration.md`  
> **Parent Hub**: [`REQUIREMENTS.md`](../../../REQUIREMENTS.md)

This document defines the functional and non-functional requirements specific to the **Shell Profiles & Application Configuration** subsystem of MultiShell. It covers profile management (PowerShell, CMD, WSL, NuShell, custom executable profiles), profile creation and editing dialogs, startup working directories, and global settings menu management.

---

## Module Overview
- **Module Name**: Profiles & Configuration
- **Scope Identifier**: `PROF` / `SET`
- **Architecture Contract**: [`docs/architecture/modules/profiles-and-configuration.md`](../../architecture/modules/profiles-and-configuration.md)
- **Primary Domain Specialist**: `None`

---

## Requirements

### `[REQ-SET-001]` Unified Settings Menu & Dynamic Theme Switching

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** a unified Settings menu (gear icon `⚙`) in the top toolbar providing dynamic Dark/Light theme switching, language selection, font size configuration, terminal profile management, Help (`F1`), and About dialog access.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Clicking the Settings gear button `⚙` displays a flyout menu with Theme toggle, Font sizes, Language, Profiles, Help, and About.
- [x] **AC-2**: Clicking the Theme option dynamically toggles between Dark and Light theme variants in real time.
- [x] **AC-3**: Clicking Profiles, Help, or About opens the corresponding modal dialog and closes the menu.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/profiles-and-configuration.md`

---

### `[REQ-PROF-001]` Configurable Startup Working Directory per Profile

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** terminal user,  
> **I want** each terminal profile to support a configurable startup working directory, with the user's profile directory (`%USERPROFILE%`) serving as the universal default, so that custom or built-in shell profiles launch in their dedicated project or home directories.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: Default terminal profiles (PowerShell, CMD, WSL, NuShell) default their `WorkingDirectory` to the user profile directory (`Environment.SpecialFolder.UserProfile`).
- [x] **AC-2**: When creating a new profile in the Profile Editor, the working directory input pre-populates with the user profile directory.
- [x] **AC-3**: When editing a profile, the configured directory is displayed and saved to `profiles.json`.
- [x] **AC-4**: A tab opened via a profile starts its shell session in that configured working directory.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/profiles-and-configuration.md`
- **Test Suite**: `MultiShell.Tests/TerminalProfileServiceTests.cs`

---

### `[REQ-PROF-002]` SSH Config Auto-Discovery & Remote Profile Management

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.2.0`

#### User Story
> **As a** systems administrator managing multiple remote servers,  
> **I want** MultiShell to automatically parse `~/.ssh/config` and surface discovered hosts as instant launch profiles,  
> **so that** I can connect to remote machines with a single click and receive automatic warning color tags on production hosts.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: **Given** a valid `~/.ssh/config` file, **When** opening the Profile menu or Command Palette, **Then** all configured SSH hosts are listed with their HostName and User.
- [ ] **AC-2**: **When** launching an SSH profile, **Then** a new tab opens running `ssh <Host>` with automatic tab title matching the host alias.
- [ ] **AC-3**: **When** a host alias contains keywords like `prod`, `production`, or `live`, **Then** the tab automatically applies an amber/red warning color accent stripe.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/profiles-and-configuration.md`

---

### `[REQ-PROF-003]` Custom Profile Environment Variables, Shell Arguments & Icons

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.2.0`

#### User Story
> **As a** power user with specialized development environments (e.g. Python venv, Rust toolchain, Node.js versions),  
> **I want** terminal profiles to support custom environment variables (`KEY=VALUE`), command-line arguments, and assigned icons (PowerShell, WSL, Ubuntu, Python, Docker),  
> **so that** tabs launch pre-configured for their specific runtime environment with clear visual distinction.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: **Given** the Profile Editor dialog, **When** configuring a profile, **Then** user can specify custom startup arguments (e.g. `-NoProfile`, `-NoExit`) and a list of key-value environment variables.
- [ ] **AC-2**: **When** selecting an icon from a preset icon gallery (PowerShell, CMD, WSL, Git, Python, Node, Docker), **Then** that icon renders on tab headers spawned from that profile.
- [ ] **AC-3**: **When** the shell process starts, **Then** configured environment variables are injected into the child process environment block before PTY creation.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/profiles-and-configuration.md`

---

### `[REQ-SET-002]` Custom Keybinding Configuration & JSON Keymap Overrides

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.3.0`

#### User Story
> **As a** user accustomed to specific keybindings from other terminals or text editors,  
> **I want** to customize keyboard shortcuts via a user-editable `keybindings.json` configuration file or in Settings,  
> **so that** all application shortcuts (tab navigation, split panes, search, palette, zoom) can be remapped without source code changes.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: **Given** default keybindings, **When** a user defines custom key mappings in `keybindings.json`, **Then** MultiShell overrides default keybindings on startup or hot reload.
- [ ] **AC-2**: **When** conflicting key combinations are detected, **Then** the settings UI displays a conflict warning and falls back safely to default.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/profiles-and-configuration.md`

---

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-SET-001` | Unified Settings Menu & Dynamic Theme Switching | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-PROF-001` | Configurable Startup Working Directory per Profile | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-PROF-002` | SSH Config Auto-Discovery & Remote Profile Management | Functional | BACKLOG | `v1.2.0` |
| `REQ-PROF-003` | Custom Profile Environment Variables, Shell Arguments & Icons | Functional | BACKLOG | `v1.2.0` |
| `REQ-SET-002` | Custom Keybinding Configuration & JSON Keymap Overrides | Functional | BACKLOG | `v1.3.0` |
