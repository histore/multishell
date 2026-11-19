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

### `[BL-QUAL-002]` Use `ProcessStartInfo.ArgumentList` in `LinkDetectionHelper.OpenResolvedLocalPath` (Review Item E) — **DONE**
- **Priority**: Low
- **Status**: `DONE` (replaced manual string interpolation `Arguments = $"\"{resolvedPath}\""` with `psi.ArgumentList.Add(resolvedPath)`)

### `[BL-QUAL-003]` Consolidate Duplicate Streaming & UI-Feed Logic in Tab/Pane ViewModels (Review Item E) — **DONE**
- **Priority**: Low
- **Status**: `DONE` (extracted shared ANSI streaming, OSC color query dispatch, and batched UI-feed coalescing into `Services/TerminalUiFeedCoordinator.cs`)

---

## General Codebase Review Findings (2026-11-19)

### Critical

#### `[BL-CRIT-001]` Fix Split-Pane Initial Session Ownership, Premature Tab Closure & Disposal Leak — **DONE**
- **Criticality**: `Critical`
- **Status**: `DONE` (routed pane session exits through `TerminalTabViewModel.HandlePaneSessionExited`, delegated active session/model calls to `ActivePane`, and ensured `Dispose()` cleans up all panes including the initial pane)
- **Scope**: `ViewModels/TerminalTabViewModel.cs`, `ViewModels/TerminalTabViewModel.Panes.cs`, `ViewModels/Panes/TerminalPaneViewModel.cs`, `ViewModels/Panes/TerminalPaneViewModel.Streaming.cs`
- **Summary**: `TerminalTabViewModel` shares `_session` with its initial pane, causing three failures: (1) exiting the initial pane in a split tab closes the entire tab via `TerminalTabViewModel.OnSessionExited`, (2) manually closing the initial pane disposes the tab's shared `_session` while other panes remain open, and (3) `TerminalTabViewModel.Dispose()` skips `initialPane.Dispose()`, leaking `_silenceTimer`, `_uiFeedCoordinator`, and event subscriptions.

#### `[BL-CRIT-002]` Fix In-Terminal Search (`Ctrl+Shift+F`, `Ctrl+Shift+F3`, `Escape`) Routing to Active Pane — **DONE**
- **Criticality**: `Critical`
- **Status**: `DONE` (delegated `TerminalTabViewModel` search commands and query updates to `ActivePane`, localized `TerminalPaneViewModel.UpdateSearchMatchSummary`, and aligned `MainWindow.Keyboard.cs` search state checks with `ActivePane.IsSearchOpen`)
- **Scope**: `Views/MainWindow.Keyboard.cs`, `ViewModels/TerminalTabViewModel.Search.cs`, `ViewModels/Panes/TerminalPaneViewModel.Search.cs`, `Views/TerminalPaneView.axaml`
- **Summary**: `SearchOverlay` exists only in `TerminalPaneView.axaml` (bound to `TerminalPaneViewModel.IsSearchOpen`), but the window-level tunnel `KeyDown` handler in `MainWindow.Keyboard.cs` intercepts `Ctrl+Shift+F` / `Ctrl+Shift+F3` / `Escape` and calls `SelectedTab.OpenSearch()` / `SearchNext()` / `CloseSearch()` without delegating to `ActivePane`, preventing the search overlay from appearing.

---

### High

#### `[BL-HIGH-001]` Harden Link Detection (UNC `//` Bypass, Tab CWD Priority, `file://`, Blocklist) & Bracketed Paste Sanitization — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (blocked forward/mixed-slash UNC prefixes in `IsSafeLocalPath`, prioritized tab `workingDirectory` for relative paths, added safe `file://` URI handling, expanded `ExecutableExtensions`, and sanitized bracketed paste breakout markers in `TerminalPaneViewModel.PasteText`)
- **Scope**: `Services/LinkDetectionHelper.cs`, `ViewModels/Panes/TerminalPaneViewModel.cs`
- **Summary**: (1) `IsSafeLocalPath` only checks `\\`, allowing forward-slash `//server/share` UNC paths to trigger outbound SMB/NetNTLM leaks; (2) `TerminalPaneViewModel.PasteText` embeds raw clipboard text into `\x1b[200~...\x1b[201~` without stripping `\x1b[201~` (bracketed paste breakout); (3) `TryResolveFilePath` resolves relative paths against the host process CWD before the tab's `workingDirectory`; (4) dangerous ShellExecute extensions (`.url`, `.chm`, `.scf`, `.py`, `.pyw`, `.msix`, `.appx`, `.iso`, `.vhd`, `.settingcontent-ms`, `.library-ms`) are missing from `ExecutableExtensions`, and `file://` URLs detected by `ExtractAllLinks` silently fail in `OpenTarget`.

#### `[BL-HIGH-002]` Fix Win32 ConPTY Handle Leaks, Use-After-Close Races & Blocking Disposal in `ShellSession` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (transferred pipe handle ownership prior to `CreatePseudoConsole`, synchronized `_pseudoConsole` disposal with `Resize()` via `_syncRoot` + `DangerousAddRef`/`DangerousRelease`, guarded `DeleteProcThreadAttributeList` initialization, handled immediate-exit PID/WaitForExit races, offloaded `WaitForExit(500)` cleanup, and invoked OSC events outside `_oscBufferLock`)
- **Scope**: `Services/ShellSession.cs`, `Services/ShellSession.Process.cs`, `Services/ShellSession.Streams.cs`
- **Summary**: (1) `CreatePseudoConsole` failure leaks `inputWriteHandle` and `outputReadHandle`; (2) `Dispose()` releases `_pseudoConsole` and `_inputStream` without acquiring `_syncRoot` / `_inputWriteLock` or atomic disposal state, racing with `Resize()`; (3) `finally` calls `DeleteProcThreadAttributeList` even if initialization failed; (4) `Process.GetProcessById` races with immediate exit and `WaitForExitAsync` misses `ObjectDisposedException`; (5) synchronous `_process.WaitForExit(500)` can stall the UI thread; (6) OSC events are invoked while holding `_oscBufferLock`.

#### `[BL-HIGH-003]` Fix Thread-Safety Races, UI-Thread Marshaling & Add Unit Tests for `TerminalUiFeedCoordinator` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (marshaled `MarkCommandStarted`/`OnSessionCommandCompleted` onto `IDispatcherService`, synchronized `_silenceTimer` disposal under `_silenceTimerLock` with `_isDisposed` guards, made `_isDisposed` volatile and preserved UI-thread flush ordering in `TerminalUiFeedCoordinator`, and added `TerminalUiFeedCoordinatorTests`)
- **Scope**: `ViewModels/TerminalTabViewModel.ExecutionState.cs`, `ViewModels/Panes/TerminalPaneViewModel.Streaming.cs`, `Services/TerminalUiFeedCoordinator.cs`, `ViewModels/TerminalTabViewModel.cs`
- **Summary**: (1) `TerminalTabViewModel.OnSessionCommandCompleted` mutates observable properties and stops `_activityCooldownTimer` directly on the PTY background thread; (2) `TerminalPaneViewModel.OnSessionCommandCompleted` disposes `_silenceTimer` outside `_silenceTimerLock` and UI callbacks omit `_isDisposed` re-checks; (3) `TerminalUiFeedCoordinator` uses non-volatile `_isDisposed`, can feed out-of-order when `CheckAccess()` is true while buffered chunks remain, is disposed after `TerminalModel.Dispose()`, and lacks dedicated unit tests.

#### `[BL-HIGH-004]` Fix Event/Visual-Tree Memory Leaks & Broken Re-Attach in Views — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (added symmetric `OnAttachedToVisualTree`/`OnDetachedFromVisualTree` re-subscription and tracked split-node `PropertyChanged` cleanup + cache eviction `DataContext = null` in `SplitPaneContainerControl`, plus named VM/tab listener cleanup in `MainWindow` and `HistoryDrawerView`)
- **Scope**: `Views/SplitPaneContainerControl.cs`, `Views/MainWindow.axaml.cs`, `Views/MainWindow.TabDropAndRename.cs`, `Views/Dialogs/HistoryDrawerView.axaml.cs`
- **Summary**: (1) `SplitPaneContainerControl` unsubscribes `LayoutChanged` in `OnDetachedFromVisualTree` without re-subscribing in `OnAttachedToVisualTree`; (2) `ConfigureVerticalSplitGrid`/`ConfigureHorizontalSplitGrid` accumulate anonymous `split.PropertyChanged` handlers on every `RebuildVisualTree()`; (3) `CleanupViewCache()` evicts `TerminalPaneView` instances without clearing `DataContext = null`; (4) `MainWindow` and `HistoryDrawerView` do not unsubscribe VM/tab event handlers on detach/close.

#### `[BL-HIGH-005]` Guard `async void` Handlers, Prevent Duplicate Satellite Tab Archiving & Forward DI in Satellite Windows — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (wrapped picker/drag/clipboard async handlers with targeted exception filters, cleaned up `_dragScrollTimer` on `Closed`, added re-entrancy protection in `WindowCoordinatorService.CloseWindowAsync` and `MainViewModel.ArchiveTabsTo`, and forwarded `_dispatcherService` + `_terminalModelFactory` in `CreateSatelliteViewModel()`)
- **Scope**: `Views/MainWindow.axaml.cs`, `Views/MainWindow.Tabs.cs`, `Views/MainWindow.Keyboard.cs`, `ViewModels/MainViewModel.MultiWindow.cs`, `Services/WindowCoordinatorService.cs`
- **Summary**: (1) Unhandled exceptions in `async void` handlers (`BrowseExecutableRequested`, `BrowseWorkingDirRequested`, `OnTabsPointerMoved`) and fire-and-forget picker/clipboard calls can crash the app; (2) closing a satellite window via `CloseWindowAsync` triggers `ArchiveTabsTo(primary)` twice (once in `CloseWindowAsync`, again in `MainWindow.Closing`), duplicating closed tabs; (3) `CreateSatelliteViewModel()` omits `_dispatcherService` and `_terminalModelFactory`.

#### `[BL-HIGH-006]` Protect Private `SvcSystems.UI.Terminal` Reflection Under NativeAOT Trimming — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (added `ILLink.Descriptors.xml` registered via `TrimmerRootDescriptor`, annotated reflection sites with `[DynamicDependency]`, and added `TerminalReflectionAotContractTests` to verify exact types and members)
- **Scope**: `Services/AvaloniaTerminalModelAdapter.cs`, `Views/TerminalPaneView.axaml.cs`, `Views/TerminalPaneView.Links.cs`
- **Summary**: Private reflection against `SvcSystems.UI.Terminal` (`_mouseTracker`, `TrackingMode`, `FallbackXtermPalette`, `ClearFormattedTextCache`, `_formattedTextCache`, `_surface`, `_consoleTextSize`) is unguarded against NativeAOT trimming (`IL2075` suppressed without `[DynamicDependency]`), risking silent runtime regressions in Release AOT builds.

#### `[BL-HIGH-007]` Fix Undefined XAML Resources, Hardcoded German Window Title & `REQ-REL-003` Release Gap — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (defined `AppTextTertiary` and `AppDangerSubtle` in Dark/Light `ThemeDictionaries` of `App.axaml`, localized `MainViewModel.WindowTitle` via `Window_Secondary_Title_Prefix` across all 6 languages with dynamic `WindowTitle` change notification, updated `WindowCoordinatorServiceTests`, and added framework-dependent lightweight archive packaging to `.github/workflows/release.yml` per `REQ-REL-003`)
- **Scope**: `App.axaml`, `Views/Controls/SettingsMenuButton.axaml`, `Styles/TabStyles.axaml`, `ViewModels/MainViewModel.Tabs.cs`, `Services/LocalizationService.*.cs`, `MultiShell.Tests/WindowCoordinatorServiceTests.cs`, `.github/workflows/release.yml`
- **Summary**: (1) `AppTextTertiary` (`SettingsMenuButton.axaml:88`) and `AppDangerSubtle` (`TabStyles.axaml:208`) are undefined in `App.axaml`; (2) `MainViewModel.WindowTitle` hardcodes German `"[Fenster {WindowNumber} ⧉] "` (and `WindowCoordinatorServiceTests.cs:241` asserts this bug under `"en"`); (3) `.github/workflows/release.yml` omits the framework-dependent lightweight archive required by `REQ-REL-003`.

---

### Medium

#### `[BL-MED-001]` Multi-Instance Persistence Temp-File Collisions, Save Debouncing & Async Load Error Handling — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (extracted `AtomicFileHelper` with GUID-unique `.tmp` files and cleanup, added 120ms save debouncing with shutdown cancellation in `MainViewModel.Persistence.cs`, and added semaphore-guarded JSON/IO error handling in `TerminalProfileService.LoadProfilesAsync`)
- **Scope**: `Services/TabStatePersistenceService.cs`, `Services/TerminalProfileService.cs`, `ViewModels/MainViewModel.Persistence.cs`
- **Summary**: Fixed `.tmp` filenames collide under `--multi-instance`; rapid `TriggerSaveState()` calls lack debouncing and can overwrite newer snapshots out of order; `TerminalProfileService.LoadProfilesAsync` lacks JSON/IO `try/catch` handling.

#### `[BL-MED-002]` Harden `SingleInstanceService` Pipe Framing, Client Security & Shutdown Ordering — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (updated `ReadTargetDirectoryAsync` to read until EOF with a 32K char cap, added `PipeOptions.CurrentUserOnly` to `NamedPipeClientStream`, waited on `_serverTask` before disposing `_cts`, and narrowed exception filters)
- **Scope**: `Services/SingleInstanceService.cs`
- **Summary**: Single `ReadAsync` call can truncate payloads; `NamedPipeClientStream` omits `PipeOptions.CurrentUserOnly`; `Dispose()` disposes `_cts` before `_serverTask` exits.

#### `[BL-MED-003]` Resolve Built-In System Shells from `System32` First & Quote Executables with `FormatWin32QuotedArgument` — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (resolved built-in system shells from Windows System directories before qualified `PATH` entries in `ShellDiscoveryService.ResolveExecutable` and quoted all shell executables with `FormatWin32QuotedArgument` in `ShellSession.Process.cs`)
- **Scope**: `Services/ShellDiscoveryService.cs`, `Services/ShellSession.Process.cs`
- **Summary**: `ResolveExecutable` searches `PATH` before `System32` for system binaries (`cmd.exe`, `wsl.exe`, `powershell.exe`), and `GenerateShellCommandLine` wraps executables with naive `"{exePath}"` instead of `FormatWin32QuotedArgument`.

#### `[BL-MED-004]` Prune Evicted Global Commands in `PathCommandHistoryService` — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (extracted `AppendCommandLocked` and `RemoveFromGlobalIfUnusedLocked` so FIFO eviction beyond `MaxHistoryPerPath` prunes orphaned entries from `_globalCommands` and `_commandTimestamps`)
- **Scope**: `Services/PathCommandHistoryService.cs`
- **Summary**: Evicting entries exceeding `MaxHistoryPerPath` never prunes `_globalCommands` or `_commandTimestamps`, allowing unbounded growth and stale global history entries.

#### `[BL-MED-005]` Define Missing `Button.tabMenuItemBtn` Style & Localize Remaining Hardcoded UI Strings
- **Criticality**: `Medium`
- **Status**: `OPEN`
- **Scope**: `Styles/TabStyles.axaml`, `Views/MainWindow.axaml.cs`, `Views/Dialogs/ProfilesModalView.axaml`, `Views/Dialogs/AboutModalView.axaml`, `Views/Dialogs/HelpModalView.axaml`, `Views/Controls/SettingsMenuButton.axaml`, `ViewModels/TerminalTabViewModel.cs`, `Services/WindowsToastNotificationService.cs`
- **Summary**: `Classes="tabMenuItemBtn"` is applied in 10 places without a matching style rule; hardcoded strings remain in file picker filters, shell type dropdowns, shortcut labels, and terminal error/exit notices.

#### `[BL-MED-006]` Bundle `LICENSE` and `THIRD_PARTY_NOTICES.md` into Release Packages (OFL-1.1 Compliance)
- **Criticality**: `Medium`
- **Status**: `OPEN`
- **Scope**: `.github/workflows/release.yml`, `installer.iss`, `MultiShell.csproj`
- **Summary**: Published directories, portable ZIPs, and installed directories omit `LICENSE` and `THIRD_PARTY_NOTICES.md` despite bundling `FiraCodeNerdFontMono-Regular.ttf` (OFL-1.1).

#### `[BL-MED-007]` Complete Clean Architecture & Modern Interop Cleanup (`[LibraryImport]`, Composition Root Defaults)
- **Criticality**: `Medium`
- **Status**: `OPEN`
- **Scope**: `Views/MainWindow.axaml.cs`, `Services/ThemeService.cs`, `Services/UiFrameworkInfo.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/TerminalTabViewModel.cs`, `ViewModels/Panes/TerminalPaneViewModel.cs`
- **Summary**: Replace legacy `[DllImport]` in `MainWindow.axaml.cs:466` with `[LibraryImport]`; move `Avalonia*` singleton fallbacks and `avares://` font URIs out of ViewModels into composition root / UI layer; decouple `ThemeService` from direct `Dispatcher.UIThread` calls.

#### `[BL-MED-008]` Replace Remaining 20 Bare `catch` Blocks Across `Services/`, `ViewModels/`, and `Program.cs`
- **Criticality**: `Medium`
- **Status**: `OPEN`
- **Scope**: `Services/SingleInstanceService.cs`, `Services/LinkDetectionHelper.cs`, `Services/FileBrowserHelper.cs`, `Services/KeyboardStateHelper.cs`, `Services/PathNormalizer.cs`, `Services/ShellDirectoryChangeDetector.cs`, `Services/StartupPathResolver.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/MainViewModel.Persistence.cs`, `ViewModels/MainViewModel.Tabs.cs`, `Program.cs`
- **Summary**: Follow up on `BL-QUAL-001` by narrowing remaining bare `catch` blocks to targeted exception filters with `Debug.WriteLine` diagnostics.

#### `[BL-MED-009]` Synchronize Architecture Specs, `.arch-sync.json`, and Resolve `REQ-TERM-013` / `REQ-UI-004` Drift
- **Criticality**: `Medium`
- **Status**: `OPEN`
- **Scope**: `docs/architecture/.arch-sync.json`, `ARCHITECTURE.md`, `docs/architecture/modules/*.md`, `REQUIREMENTS.md`, `docs/requirements/modules/*.md`
- **Summary**: `.arch-sync.json` is 7 commits behind `HEAD`; `REQ-TERM-013` collides between Scrollback Export (`REQUIREMENTS.md`) and Status Bar Breadcrumbs (production code); `REQ-UI-004` states 5 font levels instead of 7.

---

### Low

#### `[BL-LOW-001]` Migrate Hot-Path Regexes to `[GeneratedRegex]` & Add ESC Fast-Path in `ShellSession.Streams`
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `Services/ShellSession.Streams.cs`, `Services/AnsiStreamProcessor.cs`, `Services/LinkDetectionHelper.cs`, `Services/ShellSession.cs`
- **Summary**: Replace `RegexOptions.Compiled` with NativeAOT-friendly `[GeneratedRegex]`, skip `_oscBuffer.ToString()` / regex scans when chunks contain no `0x1B` byte, and remove dead `_lastExecutedCommand` field.

#### `[BL-LOW-002]` Dispose `Process.Start` Handles & Implement/Remove Ignored Toast `onActivated` Callback
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `Services/FileBrowserHelper.cs`, `ViewModels/MainViewModel.cs`, `Services/WindowsToastNotificationService.cs`
- **Summary**: Wrap `Process.Start(...)` results in `using` and resolve unqualified `explorer.exe` via `%SystemRoot%`; either wire `onActivated` or simplify `IToastNotificationService` contract.

#### `[BL-LOW-003]` Clean Up 18 Unused Localization Keys, Harmonize Modifier Names & Add IT/PT to `installer.iss`
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `Services/LocalizationService.*.cs`, `installer.iss`
- **Summary**: Prune or wire unused i18n keys, align localized modifier names (`Shift`/`Umschalt`, `Mayús`, `Maj`, `Maiusc`), and add Italian/Portuguese languages to `installer.iss`.

#### `[BL-LOW-004]` Replace Timing-Sensitive `Thread.Sleep` / `Task.Delay` in Tests & Expand `ShellSession` Test Coverage
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `MultiShell.Tests/TerminalTabExecutionStateTests.cs`, `MultiShell.Tests/MainViewModelTabTests.ProfilesAndPersistence.cs`, `MultiShell.Tests/SharedDirectoryHistoryIntegrationTests.cs`, `MultiShell.Tests/PowerShellSessionTests.cs`
- **Summary**: Replace fragile wall-clock sleeps with deterministic clock/completion signals and add tests for OSC 7/9;9, split UTF-8 streams, and CMD/NuShell/custom profile command-line generation.

#### `[BL-LOW-005]` Decompose Oversized God-Classes & Eliminate ViewModel/View Duplication
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `ViewModels/MainViewModel*.cs`, `ViewModels/TerminalTabViewModel*.cs`, `Views/MainWindow*.cs`, `Views/TerminalPaneView*.cs`
- **Summary**: Extract cohesive sub-ViewModels (e.g., `ProfilesDialogViewModel`, `HistoryOverlayViewModel`) and shared picker/clipboard helpers.

#### `[BL-LOW-006]` Use Monotonic `_tabCounter` for New Tab Titles & Avoid Per-Getter Allocation in `DisplayRootNode`
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `ViewModels/MainViewModel.Tabs.cs`, `ViewModels/TerminalTabViewModel.Panes.cs`
- **Summary**: Increment `_tabCounter` in `CreateNewTab`/`AddNewTabWithProfile` to avoid duplicate default titles after closing tabs, and cache the zoomed `LeafPaneNodeViewModel` instead of allocating on every getter read.
