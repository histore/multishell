# Workspace State Persistence

## 1. Overview & Purpose
The Workspace State Persistence module provides reliable, atomic serialization and deserialization of the MultiShell workspace session. When enabled, open tabs, their associated profiles, titles, and last-known working directories are saved so the user's workspace is seamlessly restored upon application launch.

## 2. Models & Contracts

### 2.1 Models (`Models/TabState.cs`)
* **`PersistedTabState`**:
  * Immutable record containing:
    * `ProfileId`: GUID matching the shell profile.
    * `Title`: Customized or last-resolved tab title.
    * `WorkingDirectory`: Working directory path at the moment of persistence.
    * `ProfileName`: Human-readable profile name fallback.
* **`WorkspaceState`**:
  * Immutable record containing:
    * `Tabs`: List of `PersistedTabState` entries.
    * `ActiveTabIndex`: Index of the currently focused tab.
    * `LastSaved`: UTC timestamp of the snapshot.

### 2.2 Contracts (`Services/ITabStatePersistenceService.cs`)
* **`SaveWorkspaceStateAsync(WorkspaceState state, CancellationToken ct)`**:
  * Serializes and writes workspace snapshot to disk atomically.
* **`LoadWorkspaceStateAsync(CancellationToken ct)`**:
  * Reads and deserializes saved workspace snapshot; returns default empty state if the file does not exist or is corrupted.

## 3. High-Performance AOT Serialization
* **`MultiShellJsonSerializerContext.cs`**:
  * Implements `System.Text.Json.Serialization.JsonSerializerContext`.
  * Configured with `[JsonSerializable(typeof(WorkspaceState))]` and `[JsonSerializable(typeof(TerminalProfile))]`.
  * Generates metadata at compile-time, eliminating runtime reflection for Native AOT readiness, fast startup time, and zero startup warmup penalty.

## 4. Resilience & Atomic Swap Strategy
To prevent data corruption caused by application crash or power interruption during write operations:
1. Data is serialized into memory.
2. Written to a temporary file in the target directory (`tabs_state.json.tmp`).
3. Flushed to disk with `FileStream.Flush(flushToDisk: true)`.
4. Atomically moved or replaced over `tabs_state.json` via `File.Move(..., overwrite: true)` or `File.Replace(...)`.
5. If deserialization fails (e.g. invalid JSON from an external edit), the service logs a warning, backs up the corrupt file as `.corrupt`, and safely returns an empty workspace.
