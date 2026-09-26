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

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-SET-001` | Unified Settings Menu & Dynamic Theme Switching | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-PROF-001` | Configurable Startup Working Directory per Profile | Functional | IMPLEMENTED | `v0.1.0` |
