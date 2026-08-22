# MultiShell

[![CI](https://github.com/your-username/multishell/actions/workflows/ci.yml/badge.svg)](https://github.com/your-username/multishell/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/your-username/multishell)](https://github.com/your-username/multishell/releases)
[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](LICENSE)
[![Framework: .NET 10](https://img.shields.io/badge/Framework-.NET_10-purple.svg)](https://dotnet.microsoft.com/)
[![UI: Avalonia 11.2](https://img.shields.io/badge/UI-Avalonia_11.2-red.svg)](https://avaloniaui.net/)

**MultiShell** is a high-performance, ergonomic Windows PowerShell workspace and terminal multiplexer built with **C# 13**, **.NET 10**, and **Avalonia UI 11.2**, following the principles of **Clean Architecture** and **Clean Code**.

---

## 🌟 Key Features

| Feature | Description |
| :--- | :--- |
| ⚡ **ConPTY Terminal Multiplexing** | Native Windows Pseudo Console (ConPTY) terminal engine with ANSI/VT color palette support. |
| 🎨 **Independent Dual-Theme Engine** | Switch **App UI** (Dark / Light) and **Terminal Shell** (Dark / Light) completely independently in the Settings menu (`⚙ ▾`). |
| 🌍 **Multi-Language Support (DE / EN / FR / ES)** | Dynamic UI runtime localization supporting **Deutsch**, **English** *(Default Fallback)*, **Français**, and **Español**, with automatic OS language detection and user preference persistence. |
| 🔍 **Live Fuzzy Search & Type-to-Filter** | Start typing anywhere in the History Drawer (`Ctrl+Shift+H`) to fuzzy-search commands and directories in real time with subsequence matching and scoring. |
| 📜 **Command & Directory History Drawer** | Slide-out drawer on left-edge hover or shortcut `Ctrl+Shift+H` with instant real-time filtering, arrow navigation, and `Enter` execution. |
| 🗂️ **Ergonomic 30px Tab Bar** | Pixel-perfect 30px tab bar with drag & drop reordering, overflow scrolling buttons (`‹ ›`), and quick dropdown list (`≡ ▾`). |
| 💾 **Robust Workspace Persistence** | Automatically saves open tabs, working directories, active selection, and custom language preferences across restarts. |
| ⌨️ **Comprehensive Keyboard Navigation** | First-class keyboard shortcuts for tabs (`Ctrl+Shift+T`, `Ctrl+Shift+D`, `Ctrl+W`, `Ctrl+Tab`), help (`F1`), history (`Ctrl+Shift+H`), and modals (`Esc`). |

---

## 🏗️ Architecture

MultiShell adheres strictly to **Clean Architecture**:

```
MultiShell
├── Assets/                     # Application Icons and Visual Assets
│   ├── multishell.ico          # Multi-resolution Windows application icon
│   └── multishell.png          # High-resolution branding image
├── Models/                     # Core Domain Models
│   ├── TabState.cs             # Workspace & Tab persistence records
│   └── LanguageOption.cs       # Localization language definition
├── Services/                   # Business Logic & Infrastructure Contracts
│   ├── IPowerShellSession.cs   # ConPTY / Windows PseudoConsole session
│   ├── PowerShellSession.cs    # P/Invoke kernel32 & process management
│   ├── IPowerShellProcessService.cs # PowerShell session factory contract
│   ├── PowerShellProcessService.cs  # Process service implementation
│   ├── IThemeService.cs        # Independent App & Terminal theme manager
│   ├── ThemeService.cs         # Theme implementation & dynamic styling
│   ├── ILocalizationService.cs # Dynamic multi-language service contract
│   ├── LocalizationService.cs  # Dictionaries for DE, EN, FR, ES & OS detection
│   ├── IFuzzySearchService.cs  # Subsequence fuzzy search contract
│   ├── FuzzySearchService.cs   # Score-ranked fuzzy matching engine
│   ├── ITabStatePersistenceService.cs # Workspace JSON persistence contract
│   └── TabStatePersistenceService.cs  # Thread-safe atomic JSON file store
├── ViewModels/                 # MVVM Presentation Logic (CommunityToolkit.Mvvm)
│   ├── ViewModelBase.cs        # Base ObservableObject
│   ├── MainViewModel.cs        # Orchestrates tabs, settings, and persistence
│   └── TerminalTabViewModel.cs # Tab state, history, and ConPTY bridge
└── Views/                      # Avalonia UI XAML Views & Controls
    ├── MainWindow.axaml        # Main window layout, toolbar, drawer, & modals
    ├── MainWindow.axaml.cs     # Drag-and-drop & keyboard dispatch
    ├── TerminalTabView.axaml   # Terminal tab control hosting ConPTY surface
    └── TerminalTabView.axaml.cs# Color palette synchronization & cache flushing
```

---

## 🚀 Getting Started

### Prerequisites
- Windows 10 / 11 (64-bit)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone & Run
```powershell
# Clone repository
git clone https://github.com/your-username/multishell.git
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
| `Ctrl + Shift + T` | Open new PowerShell terminal tab |
| `Ctrl + Shift + D` | Duplicate current tab (same working directory) |
| `Ctrl + W` | Close active terminal tab |
| `Ctrl + Tab` / `Ctrl + Shift + Tab` | Switch to next / previous tab |
| `Ctrl + Shift + H` | Toggle live Command & Directory History drawer |
| `F1` | Open Help & Keyboard Shortcuts guide |
| `Escape` | Clear search filter / close active overlay / dialog and focus terminal |

---

## 🧪 Testing

MultiShell includes a comprehensive xUnit test suite covering MVVM ViewModels, Process services, Localization, Dual-Theming, Fuzzy Search, and Workspace persistence:

```powershell
dotnet test MultiShell.Tests/MultiShell.Tests.csproj
```

---

## 📄 License

Distributed under the Apache-2.0 License. See [`LICENSE`](LICENSE) for details.
