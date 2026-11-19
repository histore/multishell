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

#### `[BL-MED-005]` Define Missing `Button.tabMenuItemBtn` Style & Localize Remaining Hardcoded UI Strings — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (defined `Button.tabMenuItemBtn` style in `Styles/TabStyles.axaml`, bound `ProfilesModalView`/`AboutModalView`/`HelpModalView` to existing localization keys, and localized executable file-picker filter, terminal start/exit messages, and toast exit-code text across all 6 languages)
- **Scope**: `Styles/TabStyles.axaml`, `Views/MainWindow.axaml.cs`, `Views/Dialogs/ProfilesModalView.axaml`, `Views/Dialogs/AboutModalView.axaml`, `Views/Dialogs/HelpModalView.axaml`, `Views/Controls/SettingsMenuButton.axaml`, `ViewModels/TerminalTabViewModel.cs`, `Services/WindowsToastNotificationService.cs`
- **Summary**: `Classes="tabMenuItemBtn"` is applied in 10 places without a matching style rule; hardcoded strings remain in file picker filters, shell type dropdowns, shortcut labels, and terminal error/exit notices.

#### `[BL-MED-006]` Bundle `LICENSE` and `THIRD_PARTY_NOTICES.md` into Release Packages (OFL-1.1 Compliance) — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (configured `MultiShell.csproj` to copy `LICENSE` and `THIRD_PARTY_NOTICES.md` to build and publish outputs used by Inno Setup, Portable ZIP, and Framework-Dependent ZIP)
- **Scope**: `.github/workflows/release.yml`, `installer.iss`, `MultiShell.csproj`
- **Summary**: Published directories, portable ZIPs, and installed directories omit `LICENSE` and `THIRD_PARTY_NOTICES.md` despite bundling `FiraCodeNerdFontMono-Regular.ttf` (OFL-1.1).

#### `[BL-MED-007]` Complete Clean Architecture & Modern Interop Cleanup (`[LibraryImport]`, Composition Root Defaults) — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (migrated `SetForegroundWindow` in `MainWindow.axaml.cs` to `[LibraryImport]`, decoupled `ThemeService` via `IDispatcherService`, moved `Avalonia*` defaults and `avares://` font URI out of ViewModels into `UiServiceDefaults` / `App.axaml.cs`, and added `ViewModelDecouplingTests` guard)
- **Scope**: `Views/MainWindow.axaml.cs`, `Services/ThemeService.cs`, `Services/UiFrameworkInfo.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/TerminalTabViewModel.cs`, `ViewModels/Panes/TerminalPaneViewModel.cs`
- **Summary**: Replace legacy `[DllImport]` in `MainWindow.axaml.cs:466` with `[LibraryImport]`; move `Avalonia*` singleton fallbacks and `avares://` font URIs out of ViewModels into composition root / UI layer; decouple `ThemeService` from direct `Dispatcher.UIThread` calls.

#### `[BL-MED-008]` Replace Remaining 20 Bare `catch` Blocks Across `Services/`, `ViewModels/`, and `Program.cs` — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (narrowed all remaining bare `catch` blocks across `FileBrowserHelper`, `KeyboardStateHelper`, `PathNormalizer`, `ShellDirectoryChangeDetector`, `StartupPathResolver`, `SingleInstanceService`, `LinkDetectionHelper`, `MainViewModel`, and `Program.cs` to targeted exception filters with `Debug.WriteLine` diagnostics; kept only the rethrowing `catch { ... throw; }` cleanup in `ShellSession.Start`)
- **Scope**: `Services/SingleInstanceService.cs`, `Services/LinkDetectionHelper.cs`, `Services/FileBrowserHelper.cs`, `Services/KeyboardStateHelper.cs`, `Services/PathNormalizer.cs`, `Services/ShellDirectoryChangeDetector.cs`, `Services/StartupPathResolver.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/MainViewModel.Persistence.cs`, `ViewModels/MainViewModel.Tabs.cs`, `Program.cs`
- **Summary**: Follow up on `BL-QUAL-001` by narrowing remaining bare `catch` blocks to targeted exception filters with `Debug.WriteLine` diagnostics.

#### `[BL-MED-009]` Synchronize Architecture Specs, `.arch-sync.json`, and Resolve `REQ-TERM-013` / `REQ-UI-004` Drift — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (updated `ARCHITECTURE.md`, `presentation.md`, `terminal-session.md`, and `.arch-sync.json`; registered Status Bar Breadcrumbs as `REQ-UI-010` to eliminate the `REQ-TERM-013` collision; aligned `REQ-UI-004` to 7 font levels)
- **Scope**: `docs/architecture/.arch-sync.json`, `ARCHITECTURE.md`, `docs/architecture/modules/*.md`, `REQUIREMENTS.md`, `docs/requirements/modules/*.md`
- **Summary**: `.arch-sync.json` is 7 commits behind `HEAD`; `REQ-TERM-013` collides between Scrollback Export (`REQUIREMENTS.md`) and Status Bar Breadcrumbs (production code); `REQ-UI-004` states 5 font levels instead of 7.

---

### Low

#### `[BL-LOW-001]` Migrate Hot-Path Regexes to `[GeneratedRegex]` & Add ESC Fast-Path in `ShellSession.Streams` — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (migrated hot-path regexes in `ShellSession.Streams`, `AnsiStreamProcessor`, and `LinkDetectionHelper` to `[GeneratedRegex]`, added an ESC-free fast-path in `ProcessOscBufferLocked`, and verified `_lastExecutedCommand` was already removed in `BL-HIGH-002`)
- **Scope**: `Services/ShellSession.Streams.cs`, `Services/AnsiStreamProcessor.cs`, `Services/LinkDetectionHelper.cs`, `Services/ShellSession.cs`
- **Summary**: Replace `RegexOptions.Compiled` with NativeAOT-friendly `[GeneratedRegex]`, skip `_oscBuffer.ToString()` / regex scans when chunks contain no `0x1B` byte, and remove dead `_lastExecutedCommand` field.

#### `[BL-LOW-002]` Dispose `Process.Start` Handles & Implement/Remove Ignored Toast `onActivated` Callback — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (disposed `Process.Start` handles in `FileBrowserHelper` and `MainViewModel.OpenGitHubUrl`, resolved `explorer.exe` via `%SystemRoot%`, made `onActivated` optional on `IToastNotificationService` with documented AUMID limitation, and guarded null toast inputs)
- **Scope**: `Services/FileBrowserHelper.cs`, `ViewModels/MainViewModel.cs`, `Services/WindowsToastNotificationService.cs`
- **Summary**: Wrap `Process.Start(...)` results in `using` and resolve unqualified `explorer.exe` via `%SystemRoot%`; either wire `onActivated` or simplify `IToastNotificationService` contract.

#### `[BL-LOW-003]` Clean Up 18 Unused Localization Keys, Harmonize Modifier Names & Add IT/PT to `installer.iss` — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (pruned the 16 remaining unused i18n keys across all 6 dictionaries—`About_Modal_Title` and `Help_Feature_5` were already wired in `BL-MED-005`—harmonized modifier names across DE/FR/ES/IT, updated `LocalizationService` doc comment, and added Italian/Portuguese + `0.0.1` fallback version to `installer.iss`)
- **Scope**: `Services/LocalizationService.*.cs`, `installer.iss`
- **Summary**: Prune or wire unused i18n keys, align localized modifier names (`Shift`/`Umschalt`, `Mayús`, `Maj`, `Maiusc`), and add Italian/Portuguese languages to `installer.iss`.

#### `[BL-LOW-004]` Replace Timing-Sensitive `Thread.Sleep` / `Task.Delay` in Tests & Expand `ShellSession` Test Coverage — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (injected `TimeProvider` into `TerminalTabViewModel` execution timing, replaced `Thread.Sleep`/`Task.Delay` flakiness in execution-state/persistence/history tests with `ManualTimeProvider` and `TaskCompletionSource`, and expanded `PowerShellSessionTests` for OSC 7, split multi-byte UTF-8 + ESC-free fast-path, and `CMD`/`NuShell`/`PowerShell`/`WSL`/custom command-line generation)
- **Scope**: `MultiShell.Tests/TerminalTabExecutionStateTests.cs`, `MultiShell.Tests/MainViewModelTabTests.ProfilesAndPersistence.cs`, `MultiShell.Tests/SharedDirectoryHistoryIntegrationTests.cs`, `MultiShell.Tests/PowerShellSessionTests.cs`
- **Summary**: Replace fragile wall-clock sleeps with deterministic clock/completion signals and add tests for OSC 7/9;9, split UTF-8 streams, and CMD/NuShell/custom profile command-line generation.

#### `[BL-LOW-005]` Decompose Oversized God-Classes & Eliminate ViewModel/View Duplication — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (extracted shared `Views/StoragePickerHelper.cs` and `Views/ClipboardHelper.cs` to eliminate duplicated picker/clipboard handling across `MainWindow.axaml.cs`, `MainWindow.Keyboard.cs`, and `TerminalPaneView.Links.cs`, and deduplicated string-history filtering via `RefreshFilteredStringHistory` in `TerminalTabViewModel.History.cs`)
- **Scope**: `ViewModels/MainViewModel*.cs`, `ViewModels/TerminalTabViewModel*.cs`, `Views/MainWindow*.cs`, `Views/TerminalPaneView*.cs`
- **Summary**: Extract cohesive sub-ViewModels (e.g., `ProfilesDialogViewModel`, `HistoryOverlayViewModel`) and shared picker/clipboard helpers.

#### `[BL-LOW-006]` Use Monotonic `_tabCounter` for New Tab Titles & Avoid Per-Getter Allocation in `DisplayRootNode` — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (updated `CreateNewTab`/`AddNewTabWithProfile` in `MainViewModel.Tabs.cs` to increment monotonic `++_tabCounter`, cached `_zoomedPaneNode` in `TerminalTabViewModel.Panes.cs` updated via `OnActivePaneChanged`/`OnIsZoomedChanged`, and added regression tests in `MainViewModelTabTests` and `SplitPaneTreeTests`)
- **Scope**: `ViewModels/MainViewModel.Tabs.cs`, `ViewModels/TerminalTabViewModel.Panes.cs`
- **Summary**: Increment `_tabCounter` in `CreateNewTab`/`AddNewTabWithProfile` to avoid duplicate default titles after closing tabs, and cache the zoomed `LeafPaneNodeViewModel` instead of allocating on every getter read.

---

## General Codebase Review Findings — Wave 2 (v0.28.0 Baseline)

### High

#### `[BL-HIGH-008]` (`REV2-HIGH-001`) Propagate Split-Pane (2nd+ Pane) Session Events & User Input to `TerminalTabViewModel` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (forwarded `WorkingDirectoryChanged`, `CommandExecuted`, `CommandCompleted`, output activity pulses, and `SendInput` input buffer tracking from `TerminalPaneViewModel` to `TerminalTabViewModel`, synchronized `Title` and path-bound command history on pane focus switch, and added regression test in `SplitPaneTreeTests`)
- **Scope**: `ViewModels/Panes/TerminalPaneViewModel.cs`, `ViewModels/Panes/TerminalPaneViewModel.Streaming.cs`, `ViewModels/TerminalTabViewModel.cs`, `ViewModels/TerminalTabViewModel.Panes.cs`
- **Summary**: Forward `WorkingDirectoryChanged`, `CommandExecuted`, `CommandCompleted`, and `UserInput` (`TrackInputBuffer`) from secondary split panes to `TerminalTabViewModel` so directory breadcrumbs, path color stripes, command/directory history, execution state, and long-running command toast notifications work across all split panes.

#### `[BL-HIGH-009]` (`REV2-HIGH-002`) Route `Ctrl+Shift+W` & `Ctrl+Shift+F4` Tunnel Handlers to `CloseActivePaneOrTab()` Instead of `CloseSelectedTab()` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (updated `TryHandleFunctionKeyDown` for `Key.F4` and `TryHandleTabManagementKeyDown` for `Key.W or Key.F4` in `MainWindow.Keyboard.cs` to invoke `CloseActivePaneOrTab()`)
- **Scope**: `Views/MainWindow.Keyboard.cs`, `Views/MainWindow.axaml`
- **Summary**: Update `TryHandleFunctionKeyDown` (`Key.F4`) and `TryHandleTabManagementKeyDown` (`Key.W or Key.F4`) in `MainWindow.Keyboard.cs` to invoke `CloseActivePaneOrTab()` instead of `CloseSelectedTab()`, preventing `Ctrl+Shift+W` / `Ctrl+Shift+F4` from destroying the entire tab when multiple split panes are open (`REQ-UI-006`).

#### `[BL-HIGH-010]` (`REV2-HIGH-003`) Implement Documented `Ctrl+Shift+K` (Clear Terminal Buffer) Shortcut in Keyboard Routing — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (added `ClearActiveTabBufferCommand` to `MainViewModel.Panes.cs`, wired `Ctrl+Shift+K` in `MainWindow.Keyboard.cs` (`TryHandleCtrlShiftActionKeyDown`) and `MainWindow.axaml`, and added unit test in `SplitPaneTreeTests`)
- **Scope**: `Views/MainWindow.Keyboard.cs`, `Views/Dialogs/HelpModalView.axaml`, `ViewModels/Panes/TerminalPaneViewModel.cs`
- **Summary**: Handle `Key.K` in `MainWindow.Keyboard.cs` (`TryHandleCtrlShiftActionKeyDown`) to invoke `SelectedTab.ActivePane?.ClearBuffer()` (fallback `SelectedTab.ClearBuffer()`), matching the `Ctrl+Shift+K` shortcut documented in `HelpModalView.axaml`.

#### `[BL-HIGH-011]` (`REV2-HIGH-004`) Prevent ConPTY Pipe Full Deadlock & UI-Thread Stall During `ShellSession.Dispose()` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (reordered `ShellSession.Dispose()` to detach `_pseudoConsole`, kill the child process tree, close input/output pipe handles so `conhost.exe` cannot block on a full 4 KB pipe buffer, and offload `ClosePseudoConsole` and `WaitForExit` to a background cleanup task)
- **Scope**: `Services/ShellSession.cs`, `Services/ShellSession.Streams.cs`
- **Summary**: Terminate the child process tree or close/drain the output pipe before calling `ClosePseudoConsole`, and avoid blocking the UI thread if `conhost.exe` flushes pending VT teardown output into a full 4 KB anonymous pipe after `PumpOutput` stops.

#### `[BL-HIGH-012]` (`REV2-HIGH-005`) Preserve MultiShell Prompt Hook & UTF-8 Initialization for Profile-Launched and Custom PowerShell/CMD Sessions — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (updated `ShellSession.GenerateShellCommandLine` so `ShellType.PowerShell` with `_customExecutable` still appends `-NoLogo -NoExit -EncodedCommand {encodedHook}` unless an explicit `-Command`/`-EncodedCommand`/`-File` argument is present, and `ShellType.CMD` with `_customExecutable` and no custom arguments still appends `/K "chcp 65001 >nul"`, with regression tests in `PowerShellSessionTests`)
- **Scope**: `Services/ShellSession.Process.cs`, `Services/TerminalProfileService.cs`
- **Summary**: Ensure `GenerateShellCommandLine` appends `-NoExit -EncodedCommand {encodedHook}` for `ShellType.PowerShell` (unless custom arguments already specify `-Command`/`-EncodedCommand`/`-File`) and `/K "chcp 65001 >nul"` for `ShellType.CMD` even when `_customExecutable` is provided via terminal profiles.

#### `[BL-HIGH-013]` (`REV2-HIGH-006`) Clear `_oscBuffer` After Non-OSC Escape Sequences to Preserve the ESC Fast-Path in `ShellSession.Streams.cs` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (narrowed initial fast-path in `ProcessOscBufferLocked` to `\x1b]` or trailing `\x1b` and added `TrimCompletedOscBuffer`/`FindUnterminatedOscStartIndex` so completed or non-OSC chunks clear `_oscBuffer` immediately, with regression test in `PowerShellSessionTests`)
- **Scope**: `Services/ShellSession.Streams.cs`
- **Summary**: Clear `_oscBuffer` after `ProcessOscMatches` when no incomplete `\x1b]` OSC prefix remains at the end of the buffer, and narrow the initial fast-path check to `\x1b]` (or trailing `\x1b`), preventing ordinary CSI color/cursor sequences from permanently disabling the fast-path.

#### `[BL-HIGH-014]` (`REV2-HIGH-007`) Fix Invalid `<property>` Trimmer Element in `ILLink.Descriptors.xml` & Sync `CHANGELOG.md` / `.arch-sync.json` — **DONE**
- **Criticality**: `High`
- **Status**: `DONE` (replaced unsupported `<property name="TrackingMode" />` in `ILLink.Descriptors.xml` with `preserve="all"` and explicit `get_TrackingMode`/`set_TrackingMode` methods, documented releases `v0.27.0` through `v0.28.0` in `CHANGELOG.md`, and synchronized `docs/architecture/.arch-sync.json`)
- **Scope**: `ILLink.Descriptors.xml`, `CHANGELOG.md`, `docs/architecture/.arch-sync.json`
- **Summary**: Replace the unsupported `<property name="TrackingMode" />` element under `XTerm.Input.MouseTracker` in `ILLink.Descriptors.xml` with `<method name="get_TrackingMode" />` / `<method name="set_TrackingMode" />` (or `preserve="all"`), document releases `v0.27.0`..`v0.28.0` in `CHANGELOG.md`, and advance `docs/architecture/.arch-sync.json`.

### Medium

#### `[BL-MED-010]` (`REV2-MED-001`) Preserve Leading `/` in POSIX / WSL OSC 7 Working Directory Paths — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (updated `GetOsc7Regex()` in `ShellSession.Streams.cs` to capture the leading `/` and added `NormalizeOsc7Path` to strip `/` only before a Windows drive letter `/[A-Za-z]:`, preserving `/home/...` POSIX paths with regression test in `PowerShellSessionTests`)
- **Scope**: `Services/ShellSession.Streams.cs`, `Services/PathNormalizer.cs`
- **Summary**: Capture the leading `/` in `GetOsc7Regex()` and only strip it when followed by a Windows drive letter (`/[A-Za-z]:`), preventing Linux/WSL OSC 7 paths (`file://ubuntu/home/user`) from losing their leading slash and resolving as relative Windows paths.

#### `[BL-MED-011]` (`REV2-MED-002`) Prevent Secondary Window Re-Entrancy During Primary Shutdown & Persist State on Satellite Close — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (added all secondary windows to `_closingWindows` under `_lock` before raising `WindowCloseRequested` during primary window shutdown, routed empty secondary window closures in `DockBackTabAsync`/`TransferTab` through `CloseWindowAsync`, and triggered `TriggerSaveState()` on primary when archiving tabs or unregistering a closed satellite window)
- **Scope**: `Services/WindowCoordinatorService.cs`, `Views/MainWindow.axaml.cs`
- **Summary**: Mark secondary windows as closing in `_closingWindows` before firing `WindowCloseRequested` during primary window shutdown (and tab dock-back/transfer) so active satellite tabs are not archived into `ClosedTabs`, and trigger `primary.TriggerSaveState()` when a user closes a secondary window.

#### `[BL-MED-012]` (`REV2-MED-003`) Propagate App & Terminal Theme Changes Across Coordinated Multi-Window Instances — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (added `AppThemeChanged` and `TerminalThemeChanged` events to `IThemeService` and `ThemeService`, subscribed in `MainViewModel` with unsubscription in `Dispose()`, and added multi-window theme propagation test in `WindowCoordinatorServiceTests`)
- **Scope**: `Services/IThemeService.cs`, `Services/ThemeService.cs`, `ViewModels/MainViewModel.cs`, `ViewModels/MainViewModel.Settings.cs`
- **Summary**: Add `AppThemeChanged` and `TerminalThemeChanged` events to `IThemeService` / `ThemeService` and subscribe in `MainViewModel` (unsubscribing in `Dispose()`) so theme toggles propagate immediately to all open satellite windows and their tabs.

#### `[BL-MED-013]` (`REV2-MED-004`) Synchronize In-Terminal Search State & `Escape` Routing When Switching Active Split Panes — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (called `SyncSearchStateFromPane(pane)` in `TerminalTabViewModel.SetActivePane`, updated `F3` and `Escape` checks in `MainWindow.Keyboard.cs` to evaluate `ActivePane?.IsSearchOpen ?? SelectedTab.IsSearchOpen`, and added regression test in `SplitPaneTreeTests`)
- **Scope**: `ViewModels/TerminalTabViewModel.Panes.cs`, `ViewModels/TerminalTabViewModel.Search.cs`, `Views/MainWindow.Keyboard.cs`
- **Summary**: Call `SyncSearchStateFromPane(pane)` inside `TerminalTabViewModel.SetActivePane(pane)` and ensure `TryHandleEscapeKeyDown()` does not swallow `Escape` keys in an active pane whose search overlay is closed.

#### `[BL-MED-014]` (`REV2-MED-005`) Sync Mouse `GridSplitter` Drag Changes Back to `SplitPaneNodeViewModel.SplitRatio` — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (subscribed to `splitter.DragCompleted` in `ConfigureVerticalSplitGrid` and `ConfigureHorizontalSplitGrid` in `SplitPaneContainerControl.cs` to sync `split.SplitRatio` via `TryComputeUpdatedSplitRatio`, with unit test in `SplitPaneTreeTests`)
- **Scope**: `Views/SplitPaneContainerControl.cs`, `ViewModels/Panes/SplitPaneNodeViewModel.cs`
- **Summary**: Update `split.SplitRatio` from the actual grid column/row star proportions when a mouse `GridSplitter` drag completes in `SplitPaneContainerControl`, preserving manual mouse resizes across subsequent splits, zoom toggles, and keyboard resizes.

#### `[BL-MED-015]` (`REV2-MED-006`) Validate `effectiveWorkingDir` Before `GenerateShellCommandLine` & Handle Immediate Process Exit — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (added `ResolveEffectiveWorkingDirectories` in `ShellSession.Process.cs` to validate/fallback deleted Windows working directories before calling `GenerateShellCommandLine` while preserving POSIX paths for `wsl.exe --cd`, updated `WorkingDirectory` on fallback, and updated `WaitForExitAsync` in `ShellSession.cs` to reset `_isRunning = false` and invoke `Exited(-1)` when `_process == null`)
- **Scope**: `Services/ShellSession.Process.cs`, `Services/ShellSession.cs`
- **Summary**: Resolve `effectiveWorkingDir` fallback before building the shell command line (preventing `wsl.exe --cd` from receiving a deleted Windows directory), update `WorkingDirectory` on fallback, and reset `_isRunning = false` + raise `Exited` if the child process exits before `Process.GetProcessById`.

#### `[BL-MED-016]` (`REV2-MED-007`) Enforce Credential Masking Inside `PathCommandHistoryService` & Fix Single-Quote Regex in `ShellCommandFilter` — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (applied `ShellCommandFilter.SanitizeCommandForHistory` directly inside `PathCommandHistoryService.RecordCommand` and `ImportCommandsForPath`, fixed the single-quote regex branch `'[^']*'` in `ShellCommandFilter`, migrated `ShellCommandFilter` regexes to `[GeneratedRegex]`, and added unit test in `PathCommandHistoryServiceTests`)
- **Scope**: `Services/PathCommandHistoryService.cs`, `Services/ShellCommandFilter.cs`
- **Summary**: Call `ShellCommandFilter.SanitizeCommandForHistory` directly inside `PathCommandHistoryService.RecordCommand` and `ImportCommandsForPath`, fix the unclosed single-quote branch (`'[^']*'`) in `ShellCommandFilter`, and migrate its regexes to `[GeneratedRegex]`.

#### `[BL-MED-017]` (`REV2-MED-008`) Guard `PathNormalizer` & `StartupPathResolver` Against Unsafe Remote UNC / SMB Paths — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (tightened WSL UNC prefix checks in `PathNormalizer.DirectoryExistsOrNonWindows` to `\\wsl$\` and `\\wsl.localhost\`, guarded `PathNormalizer` and `StartupPathResolver.ResolveWorkingDirectory` with `LinkDetectionHelper.IsSafeLocalPath`, and added unit tests in `StartupPathResolverTests`)
- **Scope**: `Services/PathNormalizer.cs`, `Services/StartupPathResolver.cs`
- **Summary**: Tighten `path.StartsWith(@"\\wsl")` in `PathNormalizer.DirectoryExistsOrNonWindows` to only allow `\\wsl$\` and `\\wsl.localhost\`, and guard `Directory.Exists`/`File.Exists` in `PathNormalizer` and `StartupPathResolver` with `LinkDetectionHelper.IsSafeLocalPath`.

#### `[BL-MED-018]` (`REV2-MED-009`) Isolate `MainViewModelTests` From Real `%LOCALAPPDATA%\MultiShell\tabs_state.json` & Dispose ViewModels — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (replaced all real `ShellProcessService` and default-path `TabStatePersistenceService` constructions in `MainViewModelTests` with `CreateIsolatedViewModel` using `FakeShellProcessService`, temporary state files, and `using` disposal)
- **Scope**: `MultiShell.Tests/MainViewModelTests.cs`
- **Summary**: Replace default `new TabStatePersistenceService()` and `new ShellProcessService()` usages in `MainViewModelTests` with isolated temporary persistence paths, mock process services, and `using` disposal so unit tests never read or overwrite the user's real `tabs_state.json`.

#### `[BL-MED-019]` (`REV2-MED-010`) Localize Default `"Custom Terminal"` Profile Name & Refresh Built-In Profile Names on Language Change — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (added `Profiles_Default_CustomName` to all 6 localization dictionaries, replaced hardcoded `"Custom Terminal"` in `MainViewModel.StartNewProfile()`, and subscribed `TerminalProfileService` to `ILocalizationService.LanguageChanged` to refresh the built-in CMD profile display name on runtime language changes)
- **Scope**: `ViewModels/MainViewModel.Profiles.cs`, `Services/TerminalProfileService.cs`, `Services/LocalizationService.*.cs`
- **Summary**: Replace hardcoded `"Custom Terminal"` in `MainViewModel.StartNewProfile()` with a localized resource key across all 6 languages and refresh unmodified built-in profile display names when `LanguageChanged` fires.

#### `[BL-MED-020]` (`REV2-MED-011`) Fail CI on Vulnerable NuGet Packages & Synchronize Architecture/Requirements Module Docs — **DONE**
- **Criticality**: `Medium`
- **Status**: `DONE` (updated `.github/workflows/ci.yml` to fail on vulnerable NuGet package output, enabled `NuGetAudit` in `MultiShell.Tests.csproj`, and synchronized `docs/architecture/modules/*.md`, `docs/requirements/modules/*.md`, `MainViewModel.Tabs.cs`, and `README.md` with current code and test file names)
- **Scope**: `.github/workflows/ci.yml`, `MultiShell.Tests/MultiShell.Tests.csproj`, `docs/architecture/modules/*.md`, `docs/requirements/modules/*.md`, `README.md`
- **Summary**: Fail the CI security audit step when `dotnet list package --vulnerable` outputs vulnerable packages, enable `NuGetAudit` in `MultiShell.Tests.csproj`, and align module architecture docs, requirements traceability test paths/ACs, and `README.md` with the current implementation.

### Low

#### `[BL-LOW-007]` (`REV2-LOW-001`) Replace Hardcoded Dark Theme Colors in `TerminalPaneView`, `SplitPaneContainerControl` & `MainWindow` Edge Fades — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (replaced hardcoded dark colors with `{DynamicResource AppSurfaceModal}` on `TerminalPaneView.MiniHeader`, theme-aware `LightInactiveBorderSubtleBrush` (`#CBD1E1`) in `TerminalPaneViewModel.BorderBrush`, dynamic `AppBorder` binding on `SplitPaneContainerControl` `GridSplitter`, and theme-aware `AppHeaderLeftFadeBrush`/`AppHeaderRightFadeBrush` resources in `App.axaml` and `MainWindow.axaml`)
- **Scope**: `Views/TerminalPaneView.axaml`, `ViewModels/Panes/TerminalPaneViewModel.cs`, `Views/SplitPaneContainerControl.cs`, `Views/MainWindow.axaml`, `App.axaml`
- **Summary**: Use theme-aware dynamic resources for `MiniHeader` background (`#D9161822`), inactive pane border/splitter brushes (`#252836`), and tab bar scroll edge fades (`#B30C0D12`) so Light theme renders cleanly.

#### `[BL-LOW-008]` (`REV2-LOW-002`) Select Newest Entry (Index `0`) Instead of Oldest Entry When Switching Tabs or Clearing Filter in `HistoryDrawerView` — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (updated `HistoryDrawerView.axaml.cs` and `HistoryDrawerView.Keyboard.cs` to select index `0` (`selectFirstItem: true, selectLastItem: false`) when switching history tabs or clearing filter text, matching the descending chronological order of history collections)
- **Scope**: `Views/Dialogs/HistoryDrawerView.axaml.cs`, `Views/Dialogs/HistoryDrawerView.Keyboard.cs`
- **Summary**: Because history collections are sorted descending (`OrderByDescending(x => x.LastUsedAt)`), select index `0` (`selectFirstItem: true, selectLastItem: false`) when switching history tabs or clearing filter text.

#### `[BL-LOW-009]` (`REV2-LOW-003`) Align `Drawer_Hint` / `Help_Feature_1` Shortcut Descriptions & Add Split-Pane / Undock Shortcuts to Help Modal — **DONE**
- **Criticality**: `Low`
- **Status**: `DONE` (updated `Drawer_Hint` and `Help_Feature_1` across all 6 localization dictionaries to reflect `Click/Shift+Enter Insert | Enter/Right-Click Execute`, and added `Ctrl+Shift+U`, `Alt+Shift+➔ / V`, `Alt+Shift+⬇ / H`, and `Alt+Shift+Z` to `HelpModalView.axaml`)
- **Scope**: `Services/LocalizationService.*.cs`, `Views/Dialogs/HelpModalView.axaml`
- **Summary**: Update `Drawer_Hint` and `Help_Feature_1` in all 6 languages to reflect `Click/Shift+Enter Insert | Enter/Right-Click Execute`, and add the implemented Split-Pane and Tab-Undock shortcuts to `HelpModalView.axaml`.

#### `[BL-LOW-010]` (`REV2-LOW-004`) Cancel Active Profile Edit on `Escape` Before Closing `ProfilesModal`
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `Views/MainWindow.Keyboard.cs`
- **Summary**: In `TryHandleEscapeKeyDown()`, when `ProfilesModal` is visible and `IsEditingProfile` is `true`, invoke `CancelEditProfile()` instead of immediately closing the entire modal.

#### `[BL-LOW-011]` (`REV2-LOW-005`) Validate `file://` Links in `LinkDetectionHelper.ExtractAllLinks` & Strip MSBuild `(line,col)` Suffixes
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `Services/LinkDetectionHelper.cs`
- **Summary**: Verify `IsSafeLocalPath` and `TryResolveFilePath` before highlighting `file://` URIs in `ExtractAllLinks`, and extend `StripLineNumberSuffix` to strip trailing `(line,col)` / `(line)` compiler diagnostic suffixes.

#### `[BL-LOW-012]` (`REV2-LOW-006`) Use `ImmutableSolidColorBrush` in `ThemeService`, Assert Terminal Buffer Contents in Streaming Tests & Test i18n Placeholder Parity
- **Criticality**: `Low`
- **Status**: `OPEN`
- **Scope**: `Services/ThemeService.cs`, `MultiShell.Tests/TerminalTabViewModelTests.TerminalInput.cs`, `MultiShell.Tests/LocalizationServiceTests.cs`
- **Summary**: Replace static mutable `SolidColorBrush` instances in `ThemeService` with `ImmutableSolidColorBrush`, assert actual terminal screen buffer text (and absence of `\uFFFD`) in the 6 fragmented UTF-8/OSC/CSI tests, and add a unit test verifying format placeholder (`{0}`, `{1}`) parity across all 6 languages.
