# Profiles & Shell Configuration

## 1. Overview & Purpose
MultiShell supports multiple concurrent shell profiles, including modern PowerShell Core (`pwsh`), Windows PowerShell, Command Prompt (`cmd`), WSL (Windows Subsystem for Linux) distributions, NuShell, and custom user-defined executable scripts. The Profiles subsystem manages discovery, customization, serialization, and profile activation.

## 2. Domain Models & Contracts

### 2.1 Models (`Models/`)
* **[`TerminalProfile`](file:///c:/projekte/csharp/multishell/Models/TerminalProfile.cs)**:
  * Record defining profile attributes:
    * `Id`: Unique identifier (GUID).
    * `Name`: Display name (e.g., "PowerShell 7", "Ubuntu (WSL)").
    * `Executable`: Full executable path or binary name (`pwsh.exe`, `wsl.exe`, `cmd.exe`).
    * `Arguments`: Startup arguments (e.g. `-NoLogo`).
    * `WorkingDirectory`: Initial working directory (optional; falls back to user home or active tab directory).
    * `IconKey`: Key referencing vector shell icon (e.g., `Powershell`, `Wsl`, `Cmd`, `Custom`).
    * `IsDefault`: Indicates whether this profile is launched on standard new tab action.

### 2.2 Contracts (`Services/`)
* **[`ITerminalProfileService`](file:///c:/projekte/csharp/multishell/Services/ITerminalProfileService.cs)**:
  * Exposes reactive observable collection or read-only list of available profiles.
  * APIs: `GetProfiles()`, `GetDefaultProfile()`, `SaveProfile(TerminalProfile profile)`, `DeleteProfile(Guid profileId)`, `SetDefaultProfile(Guid profileId)`.
* **[`IShellDiscoveryService`](file:///c:/projekte/csharp/multishell/Services/IShellDiscoveryService.cs)**:
  * Probes file system standard directories (e.g., `%ProgramFiles%/PowerShell`, `%LOCALAPPDATA%/Microsoft/WindowsApps`), PATH environment variable, and WSL registry entries to detect installed shells.

## 3. Persistence & Default Profile Bootstrapping

### 3.1 Initial Discovery & Seeding
When running for the first time without an existing `profiles.json`:
1. `ShellDiscoveryService` detects all available shells installed on the system.
2. `TerminalProfileService` creates default profiles for detected shells, giving priority to `pwsh.exe` (PowerShell 7) if present, followed by Windows PowerShell, WSL, and `cmd.exe`.
3. The generated profiles are written to `%LOCALAPPDATA%/MultiShell/profiles.json`.

### 3.2 File Storage
* Path: `%LOCALAPPDATA%\MultiShell\profiles.json`
* Serialized using `System.Text.Json` with source-generated contexts for Native AOT and high performance.
* Atomic file writing with temporary files ensures resilience against sudden process termination or system power loss.
