# Fuzzy Search, History Drawer & Link Detection

## 1. Overview & Purpose
MultiShell integrates quick-access developer utilities directly into the terminal experience: a fast subsequence fuzzy search engine for command and directory history, a slide-out History Drawer, and automated clickable URL/path link detection.

## 2. Components & Contracts

### 2.1 Fuzzy Search Engine (`Services/IFuzzySearchService.cs` / `FuzzySearchService.cs`)
* **Algorithm**:
  * Case-insensitive subsequence fuzzy matcher with character distance scoring.
  * Bonuses awarded for prefix matches, word boundary matches (following `/`, `\`, `-`, `_`, or whitespace), and contiguous character runs.
* **APIs**:
  * `FilterAndScore(IEnumerable<string> items, string pattern)`: Returns matched strings ordered by descending relevance score.
  * Zero-allocation optimizations where feasible to maintain low latency during keystroke-by-keystroke interactive filtering.

### 2.2 Path-Bound Command History Service (`Services/IPathCommandHistoryService.cs` / `PathCommandHistoryService.cs`)
* **Path-Bound Storage**:
  * Command history is decoupled from tab instances and bound directly to normalized filesystem directory paths.
  * Normalizes Windows paths (case-insensitive, strips non-root trailing directory separators, resolves relative paths).
  * Automatically bounds command history to a maximum of 100 entries per path using FIFO pruning for older items.
  * Preserves Most Recently Used (MRU) order without duplicates: re-executing an existing command pushes it to the newest position.
  * Ignores internal shell setup / hook commands via `TerminalTabViewModel.IsInternalConfigurationCommand`.
* **Live Multi-Tab Dynamic Synchronization**:
  * Fires `HistoryChangedForPath(normalizedPath)` whenever a command is recorded.
  * All active tabs currently in that directory immediately synchronize their `CommandHistory` and `FilteredCommandHistory`.
  * Switching working directories in a tab (`cd ...`) dynamically switches the tab's command history to the new path.
* **Exit Pruning & Persistence**:
  * Upon application shutdown, `PruneNonExistentPaths()` validates all tracked paths with `Directory.Exists(path)` and purges entries for paths that no longer exist on disk.
  * Surviving path histories are persisted into `WorkspaceState.PathCommandHistory` in `tabs_state.json`.

### 2.3 History Drawer (`Views/MainWindow.HistoryDrawer.cs`)
* **Drawer Interaction**:
  * Slide-out panel docked to the left of the main terminal workspace.
  * Toggled via left-edge dwell hover (300ms delay), toolbar button, or keyboard shortcut (`Ctrl+Shift+H`).
  * Displays two searchable tabs/sections:
    * **Command History**: Dynamic list of commands executed in the current tab's active directory path.
    * **Directory History**: Chronological list of directories visited in the current session.
  * Selecting a command sends it to the active shell; selecting a directory executes `cd "<dir>"`.

### 2.4 Link Detection (`Services/LinkDetectionHelper.cs`)
* Identifies URLs (`http://`, `https://`, `git@`, `file://`) and file paths within terminal text.
* Supports hover highlight and `Ctrl+Click` (or single-click depending on settings) to launch the link in the user's default browser or file manager.
