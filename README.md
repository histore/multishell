# MultiShell

[![CI](https://github.com/histore/multishell/actions/workflows/ci.yml/badge.svg)](https://github.com/histore/multishell/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/histore/multishell)](https://github.com/histore/multishell/releases)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Framework: .NET 10](https://img.shields.io/badge/Framework-.NET_10-purple.svg)](https://dotnet.microsoft.com/)
[![UI: Avalonia 12.1](https://img.shields.io/badge/UI-Avalonia_12.1-red.svg)](https://avaloniaui.net/)

**MultiShell** is a high-performance, ergonomic Windows PowerShell workspace and terminal multiplexer built with **C# 13**, **.NET 10**, and **Avalonia UI 12.1**, following the principles of **Clean Architecture** and **Clean Code**.

---

## 🌟 Key Features

| Feature | Description |
| :--- | :--- |
| ⚡ **ConPTY Terminal Multiplexing** | Native Windows Pseudo Console (ConPTY) terminal engine with ANSI/VT color palette, stateful UTF-8 decoding, and OSC 7/9/133 shell integration. |
| 🪟 **Terminal Split Panes (Horizontal & Vertical)** | Split any tab into horizontal or vertical sub-panes with draggable splitters and independent, persistent PTY sessions. |
| 🔀 **Tab Switcher Overlay with Process Status** | Centered visual tab switcher (`Ctrl+Tab`) with real-time process execution badges (Running `●`, Waiting for Input `○`, Exit Codes, Output Activity Pulse) and hover close buttons. |
| 🎨 **Independent Dual-Theme Engine** | Switch **App UI** (Dark / Light) and **Terminal Shell** (Dark / Light) completely independently in the Settings menu (`⚙ ▾`). |
| 🔤 **7-Level Font Scaling & Zoom** | 7 granular font sizes (10pt to 22pt, 14pt default) configurable via settings or keyboard shortcuts (`Ctrl++` / `Ctrl+-` / `Ctrl+0`). |
| 🔍 **In-Terminal Buffer Search** | Search through terminal output and scrollback history (`Ctrl+Shift+F`) with match highlighting, case sensitivity, regex support, and `Ctrl+Shift+F3` navigation. |
| 📜 **Fuzzy History & Directory Drawer** | Centered / slide-out drawer (`Ctrl+Shift+H` for Commands, `Ctrl+Shift+L` for Directories) with subsequence fuzzy matching, MRU recency, and instant execution. |
| 🌈 **Padovan Path Color Stripes** | Deterministic folder-depth color coding along tab headers with Padovan-sequence widths for immediate visual workspace orientation. |
| 🔔 **Background Process Notifications** | Native Windows desktop toast alerts when long-running background tasks (>10s) complete while the tab or application is in the background. |
| 🌍 **Multi-Language Support (6 Languages)** | Dynamic runtime localization supporting **English** *(Default Fallback)*, **Deutsch**, **Français**, **Español**, **Italiano**, and **Português**, with automatic OS detection and persistence. |
| 🗂️ **Ergonomic 30px Tab Bar** | Pixel-perfect 30px tab bar with drag & drop reordering, overflow scrolling buttons (`‹ ›`), double-click to open new tab, and tab color tagging. |
| 💾 **Robust Workspace Persistence** | Automatically saves open tabs, working directories, active pane arrangements, and custom preferences across restarts in `%LOCALAPPDATA%/MultiShell/tabs_state.json`. |

---

## 🏗️ Architecture

MultiShell adheres strictly to **Clean Architecture** and **MVVM** principles (for detailed design specifications, see [ARCHITECTURE.md](ARCHITECTURE.md)):

```
MultiShell
├── Assets/                     # Application Icons and Visual Assets
│   ├── multishell.ico          # Multi-resolution Windows application icon
│   └── multishell.png          # High-resolution branding image
├── Models/                     # Core Domain Models & Records (Zero UI Dependencies)
│   ├── TabState.cs             # Workspace & Tab persistence records
│   ├── TerminalProfile.cs      # Shell launch profile configurations
│   ├── PathColorStripe.cs      # Folder depth color stripe metadata
│   ├── ProcessExecutionState.cs# Real-time process execution status enum & records
│   ├── LanguageOption.cs       # Localization language definition
│   └── PaneSplitDirection.cs   # Split pane binary tree direction models
├── Services/                   # Application Services & Safe Win32 Interop
│   ├── IShellSession.cs / ShellSession.cs  # ConPTY / Win32 PTY interop & streams
│   ├── IShellProcessService.cs # Shell process execution factory
│   ├── ITerminalProfileService.cs # Terminal profile discovery & JSON store
│   ├── IThemeService.cs        # Independent App & Terminal theme styling
│   ├── IFontSizeService.cs     # 7-level font scaling engine
│   ├── ILocalizationService.cs # Dynamic 6-language localization & OS detection
│   ├── IFuzzySearchService.cs  # Subsequence fuzzy matching engine
│   ├── IPathCommandHistoryService.cs # Dynamic path-bound command history
│   ├── IDirectoryHistoryService.cs   # Global MRU directory history store
│   ├── IPathColorCodingService.cs    # Deterministic Padovan color stripe coder
│   ├── IToastNotificationService.cs  # Native Windows toast notification bridge
│   └── ITabStatePersistenceService.cs # Atomic temp-swap JSON persistence
├── ViewModels/                 # MVVM Presentation Logic (CommunityToolkit.Mvvm)
│   ├── ViewModelBase.cs        # ObservableObject base
│   ├── MainViewModel.cs        # Orchestrator (Tabs, Profiles, Settings, Switcher)
│   ├── TerminalTabViewModel.cs # Tab state, history, renaming, and search bridge
│   └── Panes/                  # Binary Tree Split Pane ViewModels
│       ├── IPaneNodeViewModel.cs
│       ├── LeafPaneNodeViewModel.cs
│       ├── SplitPaneNodeViewModel.cs
│       └── TerminalPaneViewModel.cs
└── Views/                      # Avalonia UI XAML Views & Controls (Compiled Bindings)
    ├── MainWindow.axaml        # Main window layout, toolbar, and tab strip
    ├── SplitPaneContainerControl.cs # Recursive binary tree split pane host
    ├── TerminalTabView.axaml   # Tab host view
    ├── TerminalPaneView.axaml  # Concrete terminal pane surface
    └── Dialogs/                # Modular overlay dialogs & drawers
        ├── TabSwitcherOverlayView.axaml  # Process status Tab Switcher HUD
        ├── HistoryDrawerView.axaml       # Fuzzy command & directory overlay
        ├── ProfilesModalView.axaml       # Shell profile launcher dialog
        ├── HelpModalView.axaml           # Keyboard shortcuts modal
        └── AboutModalView.axaml          # Technology & system info dialog
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11 (64-bit)
- [.NET 10.0 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) *(or .NET 10.0 SDK when building from source)*

### Clone & Run
```powershell
# Clone repository
git clone https://github.com/histore/multishell.git
cd multishell

# Build solution
dotnet build MultiShell.slnx

# Run automated tests
dotnet test MultiShell.Tests/MultiShell.Tests.csproj

# Launch MultiShell
dotnet run
```

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl + Shift + T` | Open new terminal tab (default profile) |
| `Ctrl + Shift + D` | Duplicate current tab (same working directory) |
| `Ctrl + Tab` / `Ctrl + Shift + Tab` | Open Unified Tab Switcher HUD / cycle tabs with real-time status |
| `Ctrl + PageDown` / `Ctrl + PageUp` | Switch to next / previous tab |
| `Ctrl + 1` .. `Ctrl + 8` | Jump directly to tab index 1 through 8 |
| `Ctrl + 9` | Jump directly to the last tab |
| `Ctrl + Shift + W` | Close active pane (or active tab when last pane) |
| `Ctrl + Shift + PageUp` / `PageDown` | Reorder / move active tab left or right |
| `Ctrl + Shift + F` | Toggle in-terminal scrollback search overlay |
| `Ctrl + Shift + F2` | Inline rename active tab |
| `Ctrl + Shift + F3` | Navigate to next search match in terminal |
| `Ctrl + Shift + H` | Toggle Command History drawer (`Ctrl+Shift+MiddleClick`) |
| `Ctrl + Shift + L` | Toggle Directory History drawer |
| `Ctrl + Shift + E` | Open active working directory in Windows File Explorer |
| `Ctrl + Shift + O` | Open file picker to paste file path into terminal |
| `Ctrl + Shift + B` | Open folder picker to paste directory path into terminal |
| `Ctrl + +` / `Ctrl + -` / `Ctrl + 0` | Increase, decrease, or reset terminal font size |
| `Ctrl + Enter` / `Shift + Enter` | Insert newline (`\n`) without executing command (multi-line scripts) |
| `F1` | Open Help & Keyboard Shortcuts guide |
| `Escape` | Close active dialog / drawer / search overlay and focus terminal |

---

## 🧪 Testing

MultiShell includes a comprehensive xUnit test suite (680+ automated tests) covering MVVM ViewModels, Process services, Split-Pane binary trees, Localization, Dual-Theming, Fuzzy Search, and Workspace persistence:

```powershell
dotnet test MultiShell.Tests/MultiShell.Tests.csproj
```

---

## 🤝 Development & Contributing

MultiShell employs a quality-gated Branching and Pull Request model adhering to Clean Architecture, Conventional Commits, and automated CI tests. For branching conventions, subagent workflows, and contribution guidelines, see [CONTRIBUTING.md](CONTRIBUTING.md).

The agent-assisted development workflows and role specifications are based on [histore/agent-skills](https://github.com/histore/agent-skills). See [AGENTS.md](AGENTS.md) for project-specific governance guidelines.

---

## 📄 License

Distributed under the Apache-2.0 License. See [`LICENSE`](LICENSE) for details.
