# Presentation & MVVM Architecture

## 1. Overview & Purpose
The Presentation layer is built on **Avalonia UI 11.2** and **CommunityToolkit.Mvvm**, implementing strict separation between UI visual rendering (XAML Views) and reactive application state (ViewModels). It uses compiled bindings (`x:DataType`), modular partial classes for complex ViewModel management, and decoupled event handling for responsive terminal interactions.

## 2. Component Structure

```
ViewModels/
├── ViewModelBase.cs                 # Base ObservableObject
├── MainViewModel.cs                 # Root orchestrator & dependency wiring
├── MainViewModel.Tabs.cs            # Tab collection & lifecycle management
├── MainViewModel.Profiles.cs        # Shell profile selection & management
├── MainViewModel.Settings.cs        # Theme, font, language, and modal states
├── MainViewModel.TabSwitcher.cs     # Quick tab switcher (Ctrl+Tab)
├── TerminalTabViewModel.cs          # Active terminal tab state & shell bridge
├── ClosedTabItemViewModel.cs        # Reopenable closed tab history item
└── TerminalProfileItemViewModel.cs  # Selectable profile item in UI

Views/
├── MainWindow.axaml                 # Root window XAML layout
├── MainWindow.axaml.cs              # Window lifecycle & backdrop setup
├── MainWindow.Tabs.cs               # Tab drag-and-drop & header interaction
├── MainWindow.Keyboard.cs           # Global keyboard shortcut routing
├── MainWindow.HistoryDrawer.cs      # History drawer slide-out animation & events
├── TerminalTabView.axaml            # Embedded terminal view control
├── TerminalTabView.axaml.cs         # Palette mapping, clipboard & focus logic
└── Dialogs/                         # Modal overlay views (Profiles, About, etc.)
```

## 3. Core ViewModels

### 3.1 `MainViewModel` (Partial Composition)
To avoid monolithic classes, `MainViewModel` is divided across functional partial files:
* **`MainViewModel.cs`**:
  * Root properties: active tab reference, window title resolution, status indicators.
  * Dependency injection constructor accepting services (`IPowerShellProcessService`, `ITerminalProfileService`, `IThemeService`, `ILocalizationService`, `ITabStatePersistenceService`, `IFontSizeService`).
* **`MainViewModel.Tabs.cs`**:
  * Manages `ObservableCollection<TerminalTabViewModel> Tabs`.
  * Commands: `NewTabCommand`, `CloseTabCommand`, `ReopenClosedTabCommand`, `DuplicateTabCommand`, `MoveTabCommand`.
  * Maintains `ClosedTabsStack` for resurrecting closed tabs (`Ctrl+Shift+T`).
* **`MainViewModel.Profiles.cs`**:
  * Profile selection dropdown list and default launch profile selection.
  * Commands for creating, editing, and deleting custom terminal profiles.
* **`MainViewModel.Settings.cs`**:
  * Manages settings drawer/overlay visibility, language selection (`SelectedLanguage` two-way bound to `ComboBox`, `AvailableLanguages`, language flags `IsGerman`, `IsEnglish`, `IsFrench`, `IsSpanish`, `IsItalian`, `IsPortuguese`), theme variant toggling, and global font sizing.
* **`MainViewModel.TabSwitcher.cs`**:
  * Quick switcher model backing `Ctrl+Tab` navigation with MRU (Most Recently Used) ordering.

### 3.2 `TerminalTabViewModel`
Backs an individual terminal tab instance:
* Encapsulates an `IShellSession` and binds to `TerminalControlModel`.
* Tracks shell lifecycle: process exit, working directory updates, active title updates.
* Maintains live history:
  * `CommandHistory`: List of executed commands captured via OSC 133 or manual tracking.
  * `DirectoryHistory`: List of visited working directories captured via OSC 7 / OSC 9;9.
* Manages fuzzy search filtering across command and directory history.
* Handles special keyboard input state (e.g. `IsAltGrActive` for international layouts).

## 4. UI Rendering & Views

### 4.1 `MainWindow.axaml` Layout
* **Top Header / Draggable Tab Bar**:
  * Custom 30px draggable title bar integrating window controls (minimize, maximize, close).
  * `ItemsControl` bound to `Tabs` with custom tab items supporting middle-click to close, right-click context menu, and active selection indication.
* **Main Terminal Host Panel**:
  * Persistent tab hosting via `Panel` with `IsVisible="{Binding IsSelected}"` binding. This retains ConPTY streams, terminal ANSI buffers, and scrollback without unmounting controls upon tab switching.
* **Left Slide-out History Drawer**:
  * Animated panel displaying searchable command and directory history.
* **Quick Tab Switcher Overlay**:
  * Modal HUD overlay displayed during `Ctrl+Tab` navigation.

### 4.2 `TerminalTabView.axaml`
* Hosts `SvcSystems.UI.Terminal.TerminalControl`.
* Maps application themes (Xterm 16 colors and 24-bit TrueColor) to terminal color palettes.
* Intercepts pointer events for link opening, right-click paste/copy, and focus transfer.

## 5. View Resolution & Native AOT Compatibility
* **[`ViewLocator`](../../../ViewLocator.cs)** implements Avalonia's `IDataTemplate`.
* Explicitly maps known ViewModel types (`TerminalTabViewModel` -> `TerminalTabView`) without reflection scanning, ensuring compatibility with Native AOT compilation.
