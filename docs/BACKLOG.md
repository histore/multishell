# MultiShell Technical Backlog

This document tracks non-urgent architectural improvements, refactoring candidates, and code-quality items identified during codebase reviews.

---

## Architecture & Clean Layering

### `[BL-ARCH-001]` Decouple ViewModels from Avalonia Framework Types (Review Item D) — **DONE**
- **Priority**: Medium
- **Status**: `DONE` (implemented via `IDispatcherService` / `AvaloniaDispatcherService`, `ITerminalModelAdapter` / `AvaloniaTerminalModelAdapter`, `UiFrameworkInfo`, and hex color/font-family string bindings)

---

## Code Quality & Maintainability

### `[BL-QUAL-001]` Replace Broad Silent `catch { }` Blocks with Targeted Handling (Review Item E) — **DONE**
- **Priority**: Low
- **Status**: `DONE` (narrowed bare catch clauses across `Services/`, `ViewModels/`, and `Views/` to expected exception types with `System.Diagnostics.Debug.WriteLine` diagnostics)

### `[BL-QUAL-002]` Use `ProcessStartInfo.ArgumentList` in `LinkDetectionHelper.OpenResolvedLocalPath` (Review Item E)
- **Priority**: Low
- **Context**: [`Services/LinkDetectionHelper.cs`](../Services/LinkDetectionHelper.cs#L308-L313) passes `Arguments = $"\"{resolvedPath}\""` with `UseShellExecute = false` when launching `notepad.exe`.
- **Proposed Action**: Switch to `psi.ArgumentList.Add(resolvedPath)` (matching `FileBrowserHelper.cs`) so Win32 argument escaping is handled uniformly.

### `[BL-QUAL-003]` Consolidate Duplicate Streaming & UI-Feed Logic in Tab/Pane ViewModels (Review Item E)
- **Priority**: Low
- **Context**: `TerminalTabViewModel.Streaming.cs` and `TerminalPaneViewModel.Streaming.cs` share very similar UI-feed buffering (`FlushPendingUiFeed`) and dispatcher helpers (`RunOnUi`).
- **Proposed Action**: Extract a shared helper or coordinator to eliminate duplicated buffering/dispatching code between tab and pane ViewModels.
