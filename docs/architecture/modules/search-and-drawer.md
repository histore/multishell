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

### 2.2 History Drawer (`Views/MainWindow.HistoryDrawer.cs`)
* **Drawer Interaction**:
  * Slide-out panel docked to the left or right of the main terminal workspace.
  * Toggled via toolbar button or shortcut (`Ctrl+H`).
  * Displays two searchable tabs/sections:
    * **Command History**: Chronological list of commands executed in the current session.
    * **Directory History**: Chronological list of directories visited in the current session.
  * Selecting a command sends it to the active shell; selecting a directory executes `cd "<dir>"`.

### 2.3 Link Detection (`Services/LinkDetectionHelper.cs`)
* Identifies URLs (`http://`, `https://`, `git@`, `file://`) and file paths within terminal text.
* Supports hover highlight and `Ctrl+Click` (or single-click depending on settings) to launch the link in the user's default browser or file manager.
