# Profiles & Shell Configuration

## 1. Overview & Purpose
MultiShell supports multiple concurrent shell profiles, including modern PowerShell Core (`pwsh`), Windows PowerShell, Command Prompt (`cmd`), WSL (Windows Subsystem for Linux) distributions, NuShell, and custom user-defined executable scripts. The Profiles subsystem manages discovery, customization, serialization, and profile activation.

## 2. Domain Models & Contracts

### 2.1 Models (`Models/`)
* **[`TerminalProfile`](../../../Models/TerminalProfile.cs)**:
  * Record defining profile attributes:
    * `Id`: Unique identifier (GUID).
    * `Name`: Display name (e.g., "PowerShell 7", "WSL (Linux)").
    * `ExecutablePath`: Full executable path or binary name (`pwsh.exe`, `wsl.exe`, `cmd.exe`, `nu.exe`).
    * `Arguments`: Startup arguments (e.g. `-NoLogo`).
    * `WorkingDirectory`: Initial working directory (optional; falls back to user home directory).
    * `IconTag`: Short badge tag displayed in tab headers and profile menus (e.g., `PS`, `CMD`, `WSL`, `NU`).
    * `ShellType`: Target `ShellType` enum (`PowerShell`, `NuShell`, `WSL`, `CMD`).
    * `IsBuiltIn`: Indicates whether this profile is a system-detected built-in profile.

### 2.2 Contracts (`Services/`)
* **[`ITerminalProfileService`](../../../Services/ITerminalProfileService.cs)**:
  * Exposes read-only list of available profiles and `ProfilesChanged` notifications.
  * APIs: `GetProfiles()`, `GetProfile(Guid id)`, `AddProfileAsync(TerminalProfile profile)`, `UpdateProfileAsync(TerminalProfile profile)`, `DeleteProfileAsync(Guid id)`, `ResetToDefaultsAsync()`, `LoadProfilesAsync(CancellationToken cancellationToken = default)`.
* **[`IShellDiscoveryService`](../../../Services/IShellDiscoveryService.cs)**:
  * Probes file system standard directories (e.g., `%SystemRoot%\System32`, `%ProgramFiles%\PowerShell`), PATH environment variable, and installed binaries to detect available shells.

## 3. Persistence & Default Profile Bootstrapping

### 3.1 Initial Discovery & Seeding
When running for the first time without an existing `terminal_profiles.json`:
1. `ShellDiscoveryService` detects all available shells installed on the system.
2. `TerminalProfileService` creates default profiles for detected shells, giving priority to `pwsh.exe` (PowerShell 7) if present, followed by Windows PowerShell, `cmd.exe`, WSL, and NuShell.
3. Customizations are persisted to `%LOCALAPPDATA%\MultiShell\terminal_profiles.json`.

### 3.2 File Storage
* Path: `%LOCALAPPDATA%\MultiShell\terminal_profiles.json`
* Serialized using `System.Text.Json` with source-generated contexts for Native AOT and high performance.
* Atomic file writing with temporary files ensures resilience against sudden process termination or system power loss.
