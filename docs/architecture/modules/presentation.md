# Presentation & MVVM Architecture

## 1. Overview & Purpose
The Presentation layer is built on **Avalonia UI 12.1** and **CommunityToolkit.Mvvm**, implementing strict separation between UI visual rendering (XAML Views) and reactive application state (ViewModels). It uses compiled bindings (`x:DataType`), modular partial classes for complex ViewModel management, and decoupled event handling for responsive terminal interactions.

## 2. Component Structure

```
Models/
├── CommandExecutionResult.cs          # Execution exit code and duration model (REQ-TERM-012)
├── PaneNavigationDirection.cs         # Left, Right, Up, Down directional focus (REQ-UI-006)
├── PaneSplitDirection.cs              # Vertical and Horizontal split orientation (REQ-UI-006)
├── PathBreadcrumbSegment.cs           # Interactive status bar breadcrumb segment (REQ-TERM-013)
├── PathColorStripe.cs                 # Deterministic folder path color stripe model (REQ-TAB-025)
└── ProcessExecutionState.cs           # Idle, Running, Succeeded, Failed state enum (REQ-TERM-012)

Services/
├── IPathColorCodingService.cs         # Path color stripe computation contract (REQ-TAB-025)
├── PathColorCodingService.cs          # Deterministic folder color coding with Padovan scaling
├── IToastNotificationService.cs       # Platform toast notification contract (REQ-TERM-012)
├── WindowsToastNotificationService.cs # Native Windows toast notification dispatcher
├── PathBreadcrumbParser.cs            # Breadcrumb segment extraction from directory paths (REQ-TERM-013)
├── KeyboardStateHelper.cs             # Native Windows keyboard state reader (CapsLock, etc.)
└── FileBrowserHelper.cs               # Platform-native file manager launcher (Windows Explorer, Finder, xdg-open)

ViewModels/
├── ViewModelBase.cs                   # Base ObservableObject
├── MainViewModel.cs                   # Root orchestrator & dependency wiring
├── MainViewModel.Tabs.cs              # Tab collection & lifecycle management
├── MainViewModel.ClosedTabs.cs        # Recently closed tab history & restore commands
├── MainViewModel.Profiles.cs          # Shell profile selection & management
├── MainViewModel.Settings.cs          # Theme, font, language, and modal states
├── MainViewModel.TabSwitcher.cs       # Quick tab switcher (Ctrl+Tab)
├── Panes/
│   ├── IPaneNodeViewModel.cs          # Binary tree node contract (IsLeaf, GetLeaves, FindParent, FindLeaf)
│   ├── LeafPaneNodeViewModel.cs       # Leaf node wrapping TerminalPaneViewModel
│   ├── SplitPaneNodeViewModel.cs      # Binary split container (direction, ratio, child nodes)
│   └── TerminalPaneViewModel.cs       # Independent pane session, ANSI processing, search & execution
├── TerminalTabViewModel.cs            # Tab state, title coordination & delegation to active pane
├── TerminalTabViewModel.Panes.cs      # Binary tree split operations, directional navigation, focus & zoom
├── TerminalTabViewModel.ExecutionState.cs # Process status, activity timers, badges & toast notifications
├── TerminalTabViewModel.Streaming.cs  # Output batching & ANSI buffer streaming
├── TerminalTabViewModel.History.cs    # Path & directory history synchronization
├── TerminalTabViewModel.Renaming.cs   # Custom renaming & color palette tagging
├── ClosedTabItemViewModel.cs          # Reopenable closed tab history item
└── TerminalProfileItemViewModel.cs    # Selectable profile item in UI

Views/
├── MainWindow.axaml                   # Root window XAML layout
├── MainWindow.axaml.cs                # Window lifecycle, breadcrumb folder picker & backdrop setup
├── MainWindow.Tabs.cs                 # Tab drag-and-drop & header interaction
├── MainWindow.TabDropAndRename.cs     # Drag-and-drop navigation & inline tab renaming
├── MainWindow.Keyboard.cs             # Global keyboard shortcut routing
├── MainWindow.HistoryDrawer.cs        # History drawer slide-out animation & events
├── SplitPaneContainerControl.cs       # Dynamic recursive renderer with Grid & GridSplitter + View Cache
├── TerminalPaneView.axaml             # Individual pane control with terminal, active border & mini toolbar
├── TerminalPaneView.axaml.cs          # Pane lifecycle & focus handling
├── TerminalPaneView.Links.cs          # Per-pane pointer link & hover detection, Ctrl+wheel zoom
├── TerminalTabView.axaml              # Tab host containing SplitPaneContainerControl
├── TerminalTabView.axaml.cs           # SplitPaneContainerControl data binding
├── TerminalFocusHelper.cs             # Active terminal focus resolution & restoration
└── Dialogs/                           # Modal overlay views (Profiles, About, HistoryDrawer)
    ├── HistoryDrawerView.axaml        # History Drawer overlay layout
    ├── HistoryDrawerView.axaml.cs     # Drawer open/toggle & filter selection sync
    ├── HistoryDrawerView.Keyboard.cs  # Drawer arrow navigation & tab cycling
    ├── HistoryDrawerView.Actions.cs   # Drawer command/directory execute & paste
    ├── TabSwitcherOverlayView.axaml   # Quick tab switcher HUD overlay layout (REQ-TAB-019, REQ-TAB-027)
    └── TabSwitcherOverlayView.axaml.cs # Quick tab switcher HUD overlay code-behind
```

## 3. Core ViewModels

### 3.1 `MainViewModel` (Partial Composition)
To avoid monolithic classes, `MainViewModel` is divided across functional partial files:
* **`MainViewModel.cs`**:
  * Root properties: active tab reference, window title resolution, status indicators.
  * Dependency injection constructor accepting services (`IPowerShellProcessService`, `ITerminalProfileService`, `IThemeService`, `ILocalizationService`, `ITabStatePersistenceService`, `IFontSizeService`, `IPathCommandHistoryService`, `IDirectoryHistoryService`, `IPathColorCodingService`).
  * Manages path command history lifecycle: migrates legacy tab command histories on load and invokes `_pathCommandHistoryService.PruneNonExistentPaths()` prior to saving state on application shutdown.
* **`MainViewModel.Tabs.cs`**:
  * Manages `ObservableCollection<TerminalTabViewModel> Tabs`.
  * Commands: `NewTabCommand`, `CloseTabCommand`, `CloseSelectedTabCommand` (`Ctrl+Shift+W` / `Ctrl+F4`), `CloseOtherTabsCommand`, `CloseTabsToRightCommand`, `ReopenClosedTabCommand`, `DuplicateTabCommand`, `MoveTabCommand`.
  * `AddNewTabWithDirectory` supports explicit index placement via an optional `insertIndex` parameter.
  * `DuplicateTabCommand` calculates `insertIndex = targetIndex + 1`, placing newly duplicated tabs directly to the right of the active tab.
  * `OpenDirectoryFromDrop` handles file/folder drop navigation into the active or target tab, or creates a new tab when `openInNewTab` (Shift key) is active or when 0 tabs exist.
  * Maintains `ClosedTabs` history with preserved `CustomTitle` and `TabColor` for restoring recently closed tabs.
  * Path Color Stripes Coordination: listens to `PropertyChanged` on tabs; recalculates `PathColorStripes` on `WorkingDirectory` changes via `UpdateTabPathColorStripes(tab)`.
* **`MainViewModel.Profiles.cs`**:
  * Profile selection dropdown list and default launch profile selection.
  * Commands for creating, editing, and deleting custom terminal profiles.
* **`MainViewModel.Settings.cs`**:
  * Manages settings drawer/overlay visibility, language selection (`SelectedLanguage` two-way bound to `ComboBox`, `AvailableLanguages`, language flags `IsGerman`, `IsEnglish`, `IsFrench`, `IsSpanish`, `IsItalian`, `IsPortuguese`), independent application and terminal theme toggling (`ToggleAppTheme`, `ToggleTerminalTheme`, `SetAppTheme`, `SetTerminalTheme` with automatic debounced `TriggerSaveState()` persistence), and global font sizing.
* **`MainViewModel.TabSwitcher.cs` (`REQ-TAB-019`, `REQ-TAB-027`)**:
  * Quick switcher model backing `Ctrl+Tab` navigation with MRU (Most Recently Used) and index ordering.
  * Visual parity with tab headers: binds real-time process execution states (`IsRunning`, `ExecutionState`, `StatusGlyph`, `StatusColor`, `LastExitCode`), output activity pulse, Padovan path color stripes, and tab accent colors to switcher rows.
  * Direct tab closure via row hover close button (`CloseTabCommand`).

### 3.2 `TerminalTabViewModel`
Backs an individual terminal tab instance and coordinates multiple split panes via modular partial classes:
* **`TerminalTabViewModel.cs`**:
  * Tab lifecycle orchestrator, window title coordination, and delegator to the active `TerminalPaneViewModel`.
  * Manages active pane reference (`ActivePane`), tab-level focus requests, and pane hierarchy root node (`DisplayRootNode`).
  * `OpenInFileBrowserCommand`: Launches the platform-native file browser (`FileBrowserHelper`) for the active pane's current `WorkingDirectory` with user profile fallback.
* **`TerminalTabViewModel.Panes.cs`**:
  * Binary tree split operations (`SplitVerticalCommand`, `SplitHorizontalCommand`, `CloseActivePaneCommand`, `ToggleZoomCommand`).
  * Directional pane focus navigation (`NavigatePane`) and tree traversal.
  * Handles auto-collapse on pane shell exit.
* **`TerminalTabViewModel.ExecutionState.cs` (`REQ-TERM-012`, `REQ-TERM-013`)**:
  * Tracks shell process execution states (`ProcessExecutionState`: `Idle`, `Running`, `Succeeded`, `Failed`).
  * Dynamic status indicator glyphs, activity timer, and tooltips displaying last exit code and command execution duration.
  * Native desktop toast notifications (`IToastNotificationService` / `WindowsToastNotificationService`) for long-running commands (>= 10s) completing while the tab or window is in the background.
  * Parses interactive status bar breadcrumbs (`PathBreadcrumbs`) via `PathBreadcrumbParser`, disabling ancestor segments during active running commands to prevent illegal concurrent directory changes.
* **`TerminalTabViewModel.Streaming.cs`**:
  * Zero-allocation ANSI stream processing via `AnsiStreamProcessor` and batched UI thread flushing (`FlushPendingUiFeed`).
* **`TerminalTabViewModel.History.cs`**:
  * Injected with `IPathCommandHistoryService` and `IDirectoryHistoryService`.
  * Binds `CommandHistory` dynamically to the active pane's current `WorkingDirectory`.
  * Synchronizes path history changes in real time across tabs sharing the directory.
  * Records commands under their originating directory (tracking pending directory changes before OSC updates).
  * Exposes filtered collections (`FilteredCommandHistory`, `FilteredDirectoryHistory`, `FilteredGlobalHistory`).
  * Commands for executing, pasting, and navigating history entries (`ExecuteHistoryCommand`, `PasteHistoryCommand`, `NavigateToHistoryDirectory`, `PasteHistoryDirectory`, `InsertPath`).
* **`TerminalTabViewModel.Renaming.cs` (`REQ-TAB-020`)**:
  * Custom Tab Renaming & Tab Color Palette Tagging.
  * Exposes `CustomTitle`, `TabColor`, `IsRenaming`, `RenameBuffer`, `HasCustomTitle`, `HasTabColor`, `TabColorBrush`.
  * `DisplayTitle` returns user-assigned custom title when set, falling back to dynamic middle-ellipsis formatted path/title.
  * Commands: `StartRenamingCommand`, `CommitRenamingCommand`, `CancelRenamingCommand`, `ResetCustomTitleCommand`, `SetTabColorCommand`.
  * Event: `FocusRenameBoxRequested` dispatched to view to select and focus the inline `TextBox`.
* **Deterministic Folder Path Color Stripes (`REQ-TAB-025`)**:
  * Exposes `PathColorStripes` collection (`IReadOnlyList<PathColorStripe>`) and `HasPathColorStripes` boolean indicator.
  * Populated with deterministic color stripes representing each directory depth level from left to right (up to 16 visible stripes).
  * Color stripes use Padovan sequence width multipliers (1, 2, 3, 4, 5, 7, 9, 12, 16, 21, 28, 37) and golden-ratio hue distribution for distinct, aesthetically harmonious identification.
* **`TerminalPaneViewModel`**:
  * Encapsulates an independent `IShellSession`, `TerminalControlModel`, `AnsiStreamProcessor`, in-terminal search, and active focus border state.
  * Exposes in-terminal search state (`REQ-TERM-006`): `IsSearchOpen`, `SearchQuery`, `SearchResultCount`, `CurrentSearchResultIndex`, and `SearchMatchSummary` (`"m/n"` or `"No results"`).
  * Commands: `OpenSearchCommand`, `CloseSearchCommand`, `ToggleSearchCommand`, `SearchNextCommand`, `SearchPreviousCommand`.

### 3.3 Split Pane Tree Architecture (`REQ-UI-006`)
* **Binary Tree Representation (`ViewModels/Panes/`)**:
  * An individual tab manages a recursive binary tree of `IPaneNodeViewModel` nodes (`LeafPaneNodeViewModel` and `SplitPaneNodeViewModel`).
  * `TerminalPaneViewModel`: Encapsulates an independent `IShellSession`, `TerminalControlModel`, `AnsiStreamProcessor`, execution state, in-terminal search, and active focus state.
  * Tree operations (`Split`, `Close`, `Navigate`, `Zoom`, `Resize`):
    * `SplitActivePane(direction)`: Replaces the target leaf with a new `SplitPaneNodeViewModel` containing the original leaf and a freshly spawned sibling leaf sharing the working directory.
    * `ClosePane(pane)`: Collapses the split node, lifting the surviving sibling up to replace the parent split node.
    * Auto-collapse on exit: When a shell session inside a pane terminates, `PaneExited` automatically removes the pane and collapses the parent split.
    * Directional navigation: `NavigatePane(direction)` performs spatial geometry calculations across leaf bounding boxes to transfer focus seamlessly with `Alt+ArrowKeys` or `F6`/`Shift+F6`.
    * Zoom/Maximize: `ToggleZoom()` isolates the active leaf node in the visual tree without destroying the underlying binary tree state (`Alt+Shift+Z`).
* **Zero-Reallocation Recursive View Host (`SplitPaneContainerControl.cs`)**:
  * Dynamically maps `SplitPaneNodeViewModel` to an Avalonia `Grid` with star row/column definitions and interactive `GridSplitter`.
  * Implements `_viewCache` dictionary reusing existing `TerminalPaneView` instances across tree reorganizations, preventing control recreation, input lag, or terminal disconnects.

## 4. UI Rendering & Views

### 4.1 `MainWindow.axaml` Layout
* **Top Header / Draggable Tab Bar**:
  * Custom 30px draggable title bar integrating window controls (minimize, maximize, close).
  * `ItemsControl` bound to `Tabs` with custom tab items supporting double-click or `Ctrl+Shift+F2` inline renaming (`TextBox.tabRenameBox`), top deterministic folder path color stripes (`ItemsControl` bound to `PathStripes`, REQ-TAB-025), bottom accent indicator bar (`TabColorBrush`), process status badge (`REQ-TERM-012`), right-click `ContextMenu` (Rename `Ctrl+Shift+F2`, Reset Name, 9-color Palette submenu, Duplicate `Ctrl+Shift+D`, Close `Ctrl+Shift+W` / `Ctrl+Shift+F4`, Close Other Tabs, Close Tabs to Right), and active selection indication.
  * Drag-and-drop support (`DragDrop.AllowDrop="True"`): dragging over tab items dynamically activates the hovered tab, and dropping files navigates or opens a new tab.
  * Drag hover auto-scrolling: hovering over overflow scroll arrow buttons (`‹` / `›`) during any active drag operation (external files or tab drag) automatically scrolls the tab bar sequentially (250ms dwell, then 320ms per tab step) to reveal hidden tabs.
* **Window KeyBindings & Keyboard Routing (`MainWindow.Keyboard.cs` & `FunctionKeyRouting.cs`)**:
  * Declarative `<Window.KeyBindings>` for quick tab actions: `Ctrl+Shift+T` (New Tab), `Ctrl+Shift+D` (Duplicate Tab), `Ctrl+Shift+W` and `Ctrl+Shift+F4` (Close Active Tab).
  * Function keys (`F1`..`F24`) are fundamentally passed through to the underlying terminal control to prevent collisions with CLI tools (Midnight Commander, htop, vim, Far Manager). MultiShell intercepts function keys strictly when pressed with `Ctrl+Shift` (`Ctrl+Shift+F1` Help Modal, `Ctrl+Shift+F2` Inline Rename, `Ctrl+Shift+F3` Search Navigation, `Ctrl+Shift+F4` Close Active Tab).
  * Tunnel preview event handler `OnWindowKeyDown` intercepts global shortcuts before the terminal emulator control consumes them, routing tab switching (`Ctrl+Tab`, `Ctrl+PageUp`/`PageDown`), tab reordering (`Ctrl+Shift+PageUp`/`PageDown`), and tab closing (`Ctrl+Shift+W`, `Ctrl+Shift+F4`).
* **Main Terminal Host Panel**:
  * Persistent tab hosting via `Panel` with `IsVisible="{Binding IsSelected}"` binding. This retains ConPTY streams, terminal ANSI buffers, and scrollback without unmounting controls upon tab switching.
  * Drag-and-drop surface (`TerminalContentArea`): dropping folders or files navigates the active terminal (or containing folder for files), and holding `Shift` opens a new tab.
* **Bottom Status Bar**:
  * Left: Process execution status glyph and live activity text (`SelectedTab.ExecutionStatusGlyph`, `SelectedTab.ProcessActivityText`).
  * Center: Interactive Path Breadcrumbs (`REQ-TERM-013`, `SelectedTab.PathBreadcrumbs`). Clicking any ancestor breadcrumb navigates the active shell (`cd <path>`). Clicking the current folder segment or dropdown opens a native folder picker dialog (`ShowFolderPickerForBreadcrumbAsync`).
  * Right: Maximized Pane restore button (`SelectedTab.ToggleZoomCommand`), Caps Lock amber badge (`KeyboardStateHelper`), Insert/Overwrite mode indicator, UTF-8 codepage badge, and active Shell Type badge.
* **Left Slide-out History Drawer**:
  * Animated panel displaying searchable command and directory history.
* **Quick Tab Switcher Overlay**:
  * Modal HUD overlay displayed during `Ctrl+Tab` navigation.

### 4.2 `TerminalTabView.axaml` & `TerminalPaneView.axaml`
* **`TerminalTabView.axaml`**: Hosts `SplitPaneContainerControl`, dynamically rendering the tab's recursive pane hierarchy (`DisplayRootNode`).
* **`TerminalPaneView.axaml`**: Hosts `SvcSystems.UI.Terminal.TerminalControl`, active pane border (`BorderBrush`/`BorderThickness`), floating top-right hover mini toolbar (split vertical/horizontal, zoom toggle, close pane), floating `SearchOverlay` border (search input box, match count summary, navigation and close buttons), and link hover adorner (`HoverLinkBorder`).
* Maps application themes (Xterm 16 colors and 24-bit TrueColor) to terminal color palettes. `ApplyTerminalTheme` updates `TerminalScope.Resources`, flushes `Terminal.ClearFormattedTextCache()`, and invalidates visual tree elements (`Terminal`, visual children, and `TerminalSurface`) to guarantee immediate redraw.
* Intercepts pointer events for link opening, right-click paste/copy, and focus transfer.

### 4.3 Terminal Focus Management & Restoration (`TerminalFocusHelper`)
* Solves focus loss when dismissing overlays (History Drawer, Settings modal, Profile dialog) or switching tabs.
* Traverses the visual tree to resolve the active `TerminalControl` matching the selected `TerminalTabViewModel`.
* Prevents accidentally targeting hidden/background tabs when multiple tab views exist simultaneously in the visual panel.
* Leverages `ResolveActiveControl` with `IsEffectivelyVisible` validation, falling back gracefully to the first visible terminal instance if no direct match is found.

## 5. View Resolution & Native AOT Compatibility
* **[`ViewLocator`](../../../ViewLocator.cs)** implements Avalonia's `IDataTemplate`.
* Explicitly maps known ViewModel types (`TerminalTabViewModel` -> `TerminalTabView`) without reflection scanning, ensuring compatibility with Native AOT compilation.

## 6. Startup, Single-Instance & CLI Integration
* **`Program.cs` & `SingleInstanceService`**:
  * On launch, verifies `SingleInstanceService.IsFirstInstance` using `Local\MultiShell_SingleInstance_Mutex` unless `--multi-instance`, `--new-instance`, or `-m` is passed.
  * If a secondary instance is detected, it resolves CLI arguments via `StartupPathResolver` (converting folders or file paths into target working directories) and transmits the path through the user-isolated pipe (`MultiShell_IPC_Pipe_<UserName>`) with backward-compatibility fallback to `MultiShell_IPC_Pipe` via `SendArgsToFirstInstanceAsync`, exiting immediately (ExitCode 0).
  * If the first instance, it starts the background Named Pipe server (`StartServer()`) and passes any initial directory into `App` / `MainViewModel`.
* **`App.axaml.cs`**:
  * Subscribes to `SingleInstanceService.DirectoryOpenRequested`.
  * Dispatches incoming path requests to the UI thread, bringing `MainWindow` to the foreground via `BringToForeground()` (including Win32 `SetForegroundWindow` fallback) and invoking `MainViewModel.AddNewTabWithDirectory(...)`.
