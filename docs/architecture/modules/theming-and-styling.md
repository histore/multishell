# Theming, Palettes & Typography

## 1. Overview & Purpose
MultiShell provides a modern, high-contrast visual experience optimized for developers. It supports full Avalonia Dark/Light theme switching, customizable terminal color palettes (16-color ANSI and 24-bit TrueColor), dynamic font scaling, and modern typography.

## 2. Services & Contracts

### 2.1 Theming (`Services/IThemeService.cs`)
* **`CurrentTheme`**: Active theme variant (`ThemeVariant.Dark` or `ThemeVariant.Light`).
* **`SetTheme(ThemeVariant variant)`**: Updates root application theme and recomputes terminal color palette brushes.
* Synchronizes terminal foreground, background, cursor, selection, and ANSI color table with the application theme.

### 2.2 Font Size Scaling (`Services/IFontSizeService.cs`)
* Manages terminal font size (`Min: 8pt`, `Max: 48pt`, `Default: 14pt`).
* APIs: `IncreaseFontSize()`, `DecreaseFontSize()`, `ResetFontSize()`, `SetFontSize(double size)`.
* Triggers real-time font size update on all open `TerminalTabView` instances.
* Persists user font size preference across sessions.

## 3. Terminal Palettes & TrueColor Support

### 3.1 Color Systems
* **16-Color ANSI Table**:
  * Standard 8 colors (Black, Red, Green, Yellow, Blue, Magenta, Cyan, White) and 8 high-intensity (Bright) counterparts.
  * Dynamically tuned for both Dark and Light themes to preserve high readability and contrast.
* **24-bit TrueColor**:
  * ConPTY output streams with 24-bit RGB escape codes (`\x1b[38;2;R;G;Bm`) are parsed directly by the terminal renderer.

### 3.2 Font Fallback Hierarchy
The application configures the following font priority to guarantee support for Powerline glyphs, developer icons (Nerd Fonts), and box-drawing characters:
```
Cascadia Code NF, Cascadia Mono NF, Cascadia Code, Cascadia Mono, Consolas, Segoe UI Symbol, DejaVu Sans Mono, monospace
```
