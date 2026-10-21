# Theming, Palettes & Typography

## 1. Overview & Purpose
MultiShell provides a modern, high-contrast visual experience optimized for developers. It supports full Avalonia Dark/Light theme switching, customizable terminal color palettes (16-color ANSI and 24-bit TrueColor), dynamic font scaling, and modern typography.

## 2. Services & Contracts

### 2.1 Theming (`Services/IThemeService.cs`)
* **Independent App & Terminal Theming**:
  * Decouples the application UI shell (title bar, tabs, menus, drawers) from terminal console surfaces:
    * `IsDarkAppTheme`: Active application UI theme state (`true` for Dark, `false` for Light).
    * `IsDarkTerminalTheme`: Active terminal emulator theme state (`true` for Dark, `false` for Light).
  * **`SetAppTheme(bool isDark)`**: Updates application theme variant (`Application.Current.RequestedThemeVariant`).
  * **`SetTerminalTheme(bool isDark)`**: Updates terminal color palette brushes (`SvcSystems.UI.TerminalColor{0..15}`) and base terminal colors (Foreground, Background, Cursor, Selection).
  * **`ToggleAppTheme()`** / **`ToggleTerminalTheme()`**: Toggles UI or terminal themes independently between Dark and Light.
* **Surface Invalidation & Cache Reset (`TerminalPaneView.ApplyTerminalTheme`)**:
  * Injects updated palette resources directly into `TerminalScope.Resources`.
  * Calls `Terminal.ClearFormattedTextCache()` to flush cached text layout metrics.
  * Explicitly triggers visual invalidation on `Terminal`, all visual children, and the underlying rendering surface (`TerminalSurface`) to guarantee immediate visual refresh.

### 2.2 Font Size Scaling (`Services/IFontSizeService.cs` & `Services/FontSizeService.cs`)
* **7-Level Discrete Scaling Architecture**:
  * 7 levels (1 to 7) with Level 4 as default baseline (App: 1.00x, Terminal: 14.0 pt).
  * App UI scaling levels: `[0.80, 0.88, 0.94, 1.00, 1.08, 1.18, 1.30]`.
  * Terminal font size levels: `[10.0, 11.0, 12.5, 14.0, 16.0, 18.5, 21.0]` pt.
* **APIs**:
  * `SetAppFontSizeLevel(int level)` / `SetTerminalFontSizeLevel(int level)`: Updates active levels with clamping to `[1, 7]`.
  * `IncreaseAppFontSize()`, `DecreaseAppFontSize()`, `IncreaseTerminalFontSize()`, `DecreaseTerminalFontSize()`, `ResetLevels()`.
* Triggers real-time font size update on all open `TerminalTabView` and `TerminalPaneView` instances.
* Persists user font size level preferences across sessions in `WorkspaceState`.

## 3. Terminal Palettes & TrueColor Support

### 3.1 Color Systems & Palette Propagation
* **16-Color ANSI Table**:
  * Standard 8 colors (Black, Red, Green, Yellow, Blue, Magenta, Cyan, White) and 8 high-intensity (Bright) counterparts.
  * Pre-configured palettes defined in `Services/ThemeService.cs` (`DarkTerminalPalette` and `LightTerminalPalette`), tuned for maximum readability and contrast.
  * Injected directly into `Application.Current.Resources` under keys `SvcSystems.UI.TerminalColor0` through `TerminalColor15`.
* **24-bit TrueColor**:
  * ConPTY output streams with 24-bit RGB escape codes (`\x1b[38;2;R;G;Bm`) are parsed directly by the terminal renderer.

### 3.2 Font Fallback Hierarchy
The application configures the following font priority to guarantee support for Powerline glyphs, developer icons (Nerd Fonts), and box-drawing characters:
```
Cascadia Code NF, Cascadia Mono NF, Cascadia Code, Cascadia Mono, Consolas, Segoe UI Symbol, DejaVu Sans Mono, monospace
```
