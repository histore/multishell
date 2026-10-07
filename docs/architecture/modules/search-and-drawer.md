# Fuzzy Search, History Drawer & Link Detection

## 1. Overview & Purpose
MultiShell integrates quick-access developer utilities directly into the terminal experience: a fast subsequence fuzzy search engine for command and directory history, a slide-out History Drawer, and automated clickable URL/path link detection.

## 2. Components & Contracts

### 2.1 Fuzzy Search Engine (`Services/IFuzzySearchService.cs` / `FuzzySearchService.cs`)
* **Algorithm**:
  * Case-insensitive subsequence fuzzy matcher with character distance scoring.
  * Bonuses awarded for prefix matches, word boundary matches (following `/`, `\`, `-`, `_`, or whitespace), and contiguous character runs.
* **APIs**:
  * `FilterAndRank<T>(items, pattern, textSelector)`: Returns matched items ordered by descending relevance score.
  * `FilterAndRank<T, TSecondary>(items, pattern, textSelector, secondaryKeySelector, secondaryDescending)`: Orders items by descending relevance score, breaking equal-score ties via secondary key ordering (e.g., `LastUsedAt` descending).
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

### 2.3 Shared Directory History Service (`Services/IDirectoryHistoryService.cs` / `DirectoryHistoryService.cs`)
* **Unified Global Storage**:
  * Directory history is shared across all open terminal tabs via `IDirectoryHistoryService`.
  * Normalized filesystem directory paths are stored in chronological MRU order.
  * Automatically bounds directory history to a maximum of 100 entries using FIFO eviction for older items.
  * Preserves Most Recently Used (MRU) ordering without duplicates: visiting an existing path removes its previous occurrence and places it at the newest position.
* **Live Multi-Tab Dynamic Synchronization**:
  * Fires `HistoryChanged` whenever a directory is visited or changed.
  * All active tabs immediately synchronize their `DirectoryHistory` and `FilteredDirectoryHistory`.
* **Persistence**:
  * Persisted into `WorkspaceState.SharedDirectoryHistory` in `tabs_state.json`.

### 2.4 History Overlay (`Views/Dialogs/HistoryDrawerView.axaml` & `MainWindow.HistoryDrawer.cs`)
* **Modular Implementation**:
  * `HistoryDrawerView.axaml.cs`: Lifecycle, toggle visibility, filter selection synchronization (`WireFilterSelectionSync`), and auto-select on open (`FocusActiveHistoryList`).
  * `HistoryDrawerView.Keyboard.cs`: Keyboard navigation (`Up`/`Down` item navigation, `Tab`/`Left`/`Right` tab cycling, `Escape` filter clearing/closing).
  * `HistoryDrawerView.Actions.cs`: Item execution (`Enter` / Click) and paste operations (`Shift+Enter` / Right-click) for Commands, Directories, and Global entries.
* **Overlay Interaction**:
  * Centered modal floating overlay with dark backdrop over the terminal workspace (`Margin="32"`, `MaxWidth="780"`, `MaxHeight="520"`).
  * Toggled via `Ctrl+Shift` + middle mouse click (scroll wheel click) anywhere on the terminal surface or workspace, toolbar button, or dedicated keyboard shortcuts (`Ctrl+Shift+H` for Command History, `Ctrl+Shift+L` for Directory History).
  * Dismissed by pressing `Escape`, clicking the header close button ("✕"), or clicking anywhere on the outer semi-transparent backdrop outside the dialog.
  * Displays three searchable tabs/sections (all sorted from newest to oldest, with default selection on the newest entry upon opening, and equal search score ties resolved by recency):
    * **Command History**: Commands executed in the current tab's active directory path, ordered from newest to oldest with recency tie-breaking on equal search scores.
    * **Directory History**: Unified list of directories visited across all tabs in the session (capped at 100 MRU entries), ordered from newest to oldest with recency tie-breaking on equal search scores.
    * **Global History**: Unified feed across all commands and visited directories, ordered from newest to oldest with recency tie-breaking on equal search scores.
  * Selecting a command sends it to the active shell; selecting a directory executes `cd "<dir>"`.

### 2.5 Link Detection (`Services/LinkDetectionHelper.cs`)
* Identifies URLs (`http://`, `https://`, `git@`, `file://`) and file paths within terminal text.
* Supports hover highlight and `Ctrl+Click` (or single-click depending on settings) to launch the link in the user's default browser or file manager.

