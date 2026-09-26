# Module Requirements: Terminal Session & Shell Execution

> **Specification Path**: `docs/requirements/modules/terminal-session.md`  
> **Parent Hub**: [`REQUIREMENTS.md`](../../../REQUIREMENTS.md)

This document defines the functional and non-functional requirements specific to the **Terminal Session & Shell Execution** subsystem of MultiShell. It encompasses Windows ConPTY process hosting, UTF-8 character decoding, ANSI/VT100 escape sequence processing, hyperlinks, text selection/clipboard interactions, font glyph rendering, and CLI startup activation.

---

## Module Overview
- **Module Name**: Terminal Session & Shell Execution
- **Scope Identifier**: `TERM` / `TAB`
- **Architecture Contract**: [`docs/architecture/modules/terminal-session.md`](../../architecture/modules/terminal-session.md)
- **Primary Domain Specialists**: `PerformanceOptimizer`, `SecurityAuditor`

---

## Requirements

### `[REQ-TAB-002]` Tab Closure & Bidirectional Process Lifecycle

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** tabs and their underlying shell processes to be bidirectionally tied together: when the shell process ends, the tab closes automatically; when a tab is closed, the shell process is terminated cleanly.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active tab with a running shell process, **When** the shell process terminates (e.g. user enters `exit` or process ends), **Then** the tab is automatically closed and removed from the tab bar.
- [x] **AC-2**: **When** the user clicks the `✕` close button on a tab header, **Then** the application attempts to terminate the underlying shell process.
- [x] **AC-3**: **Given** process termination succeeds, **Then** the tab is removed from the tab bar, and if the closed tab was selected, an adjacent tab is selected.
- [x] **AC-4**: **Given** process termination fails or is not possible, **Then** the tab remains open without corrupting state.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TAB-003]` Isolated PowerShell Execution & Streaming

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** each tab to run an isolated PowerShell instance (`pwsh.exe` with fallback to `powershell.exe`) with live terminal output and input streaming.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active tab, **When** commands are entered, **Then** the command is piped to standard input of the dedicated PowerShell process via ConPTY.
- [x] **AC-2**: **Given** command execution, **Then** output and ANSI sequences are streamed in real time to the terminal display.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/PowerShellProcessServiceTests.cs`

---

### `[REQ-TAB-007]` True Terminal Emulation via ConPTY (PowerShell)

- **Status**: `IMPLEMENTED`
- **Type**: `Architecture`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** each terminal tab to behave like a real terminal window so that PowerShell runs as an authentic interactive REPL with PSReadLine, syntax highlighting, TAB completion, and history navigation.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** a terminal tab is opened, **When** the tab loads, **Then** PowerShell starts as an interactive REPL process via Windows ConPTY.
- [x] **AC-2**: **Then** the terminal renders using `SvcSystems.UI.Terminal`, all keyboard input is passed directly to the PTY, and ANSI escape sequences, colors, and cursor positioning render natively.
- [x] **AC-3**: **When** the terminal window is resized, **Then** the PTY dimensions are updated and the shell reflows accordingly.
- [x] **AC-4**: **When** the tab is closed, **Then** the ConPTY session and PowerShell process are terminated cleanly.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/PowerShellProcessServiceTests.cs`

---

### `[REQ-TAB-008]` Working Directory (CWD) Tracking & Path Tab Titles

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** the application to track the active working directory of each terminal tab in real time so that tab headers display the current path with middle ellipsis (`...`) when space is limited and state can be saved.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** a running PowerShell terminal tab, **When** the directory changes (e.g. via `cd` or `Set-Location`), **Then** the shell emits an OSC 9;9 or OSC 7 escape sequence with the new path.
- [x] **AC-2**: **Then** the terminal session intercepts the sequence, updates its `WorkingDirectory` property, and triggers `WorkingDirectoryChanged`.
- [x] **AC-3**: **Then** the tab viewmodel updates its `Title` to the full path, and the tab header renders the title with middle ellipsis when space is constrained.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TAB-017]` Terminal Text Selection, Copy (Right-Click / Ctrl+C), and Paste (Right-Click / Ctrl+V)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** to select text within terminal tabs, copy selections to the clipboard via Right-Click or Ctrl+C, and paste clipboard content via Right-Click (when no selection is active) or Ctrl+V.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active terminal tab in MultiShell, **When** the user selects text using the pointer/mouse, **Then** the selected characters are visually highlighted with a high-contrast selection brush (`SelectionBrush`).
- [x] **AC-2**: **When** right-clicking on the terminal while text is selected, **Then** the selected text is copied to the system clipboard and the selection is cleared.
- [x] **AC-3**: **When** pressing `Ctrl+C` while text is selected, **Then** the selected text is copied to the system clipboard and no interrupt sequence (`\x03`) is sent to the shell.
- [x] **AC-4**: **When** pressing `Ctrl+C` without any active selection, **Then** the interrupt sequence (`\x03` / SIGINT) is passed to the underlying shell session.
- [x] **AC-5**: **When** right-clicking on the terminal without any active selection, **Then** the current text content of the system clipboard is pasted into the terminal at the cursor position.
- [x] **AC-6**: **When** pressing `Ctrl+V`, **Then** the current text content of the system clipboard is pasted into the terminal at the cursor position.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-001]` Robust UTF-8 Character Streaming & Box-Drawing Monospace Glyph Rendering

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** graphical characters (such as box-drawing borders `─`, `│`, `┌`, `┐`, `└`, `┘`, powerline glyphs, emojis, and international characters) to render crisply without character corruption, missing glyph replacement blocks, or swallowed characters across stream chunk boundaries.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** terminal output streams containing multi-byte UTF-8 sequences split across arbitrary buffer chunk boundaries, **When** chunks are processed by `ShellSession` and `TerminalTabViewModel`, **Then** stateful UTF-8 decoders preserve partial byte sequences across reads without emitting `\uFFFD` replacement blocks.
- [x] **AC-2**: **When** PowerShell or CMD shell processes are spawned, **Then** console encoding is initialized to UTF-8 (`[Console]::OutputEncoding = UTF8`, `$OutputEncoding = UTF8`, `chcp 65001`, `PYTHONIOENCODING=utf-8`).
- [x] **AC-3**: **When** `TerminalControl` renders text in `TerminalTabView`, **Then** a dedicated monospace font family chain (`FiraCode Nerd Font Mono, Cascadia Mono, Cascadia Code, Consolas, DejaVu Sans Mono, monospace`) is configured for complete box-drawing character glyph coverage.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-002]` Multi-line Newline Insertion via `Ctrl+Enter` and `Shift+Enter`

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user writing multi-line PowerShell scripts or commands,  
> **I want** `Ctrl+Enter` (and `Shift+Enter`) to insert a newline / line continuation (`\n`) without immediately executing the command.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active terminal session, **When** the user presses `Ctrl+Enter` or `Shift+Enter`, **Then** a linefeed (`\n` / `0x0A`) is sent to the ConPTY shell instead of carriage return (`\r` / `0x0D`).
- [x] **AC-2**: **Then** the shell enters a multi-line continuation prompt without executing the command prematurely.
- [x] **AC-3**: **When** the user presses standard `Enter` (without modifiers), **Then** carriage return (`\r`) is sent and the command is executed as usual.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-003]` Terminal Scrollback & Buffer Control Shortcuts

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** standard keyboard shortcuts (`Shift+PageUp`, `Shift+PageDown`, `Ctrl+Shift+K`, `Ctrl+Shift+C`, `Ctrl+Shift+V`) to navigate and manage the terminal scrollback buffer.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active terminal tab with scrollback history, **When** pressing `Shift+PageUp` or `Shift+PageDown`, **Then** the viewport scrolls through previous output.
- [x] **AC-2**: **When** pressing `Ctrl+Shift+K`, **Then** the terminal buffer is cleared.
- [x] **AC-3**: **When** pressing `Ctrl+Shift+C` or `Ctrl+Shift+V`, **Then** text is copied or pasted without interfering with Unix signals.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-004]` Multi-Chunk ANSI/VT100 Sequence Preservation & Color Bleed Prevention

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user running interactive CLI/TUI applications (e.g. GitHub Copilot CLI, Neovim with Markdown/Tree-sitter highlighting, Ink, Bubbletea),  
> **I want** ANSI escape sequences (CSI colors, cursor positioning, SGR resets, OSC sequences) to be processed cleanly across arbitrary stream chunk boundaries without fragments printing as raw text or causing color bleeding across entire text blocks.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active terminal tab in MultiShell streaming high-throughput or chunked output, **When** an ANSI/VT100 escape sequence is split across buffer chunk boundaries, **Then** the terminal stream sanitizer buffers the incomplete sequence header until the final terminator byte arrives.
- [x] **AC-2**: **Then** only complete, valid sequences or clean text chunks are passed to the terminal model (`TerminalModel.Feed()`).
- [x] **AC-3**: **Then** no stray escape codes, orphan brackets, or parameter fragments are printed to the terminal screen.
- [x] **AC-4**: **Then** background colors and text styles reset promptly at token boundaries without bleeding into following paragraphs or lines.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-005]` Clickable Hyperlinks & Local File Paths via `Ctrl+Click`

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user,  
> **I want** Web URLs (`http://`, `https://`) and local file paths (`C:\...`, relative paths, line numbers `:42`) in terminal output to be interactively clickable via `Ctrl + Left-Click`, automatically opening the URL in my default browser or the file in my default editor.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active terminal tab in MultiShell displaying command output containing links or file paths, **When** the user holds `Ctrl` and left-clicks on an `http://` or `https://` URL (or text selection containing a URL), **Then** the URL is launched in the default system web browser.
- [x] **AC-2**: **When** the user holds `Ctrl` and left-clicks on an existing file path (absolute or relative to the tab's working directory, with or without `:line` suffix), **Then** the file is opened with the system default application or editor.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-009]` Embedded Monospace Nerd Font (FiraCode Nerd Font Mono)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** terminal user,  
> **I want** an embedded monospace Nerd Font (`FiraCode Nerd Font Mono`) bundled directly into the application assets, so that powerline symbols, git branch icons, folder glyphs, and developer prompts render flawlessly without requiring manual font installation on the host OS.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** a clean Windows installation without custom Nerd Fonts installed, **When** starting MultiShell, **Then** `TerminalTabViewModel.TerminalFontFamily` resolves the embedded `avares://MultiShell/Assets/Fonts#FiraCode Nerd Font Mono` resource with fallback to system fonts.
- [x] **AC-2**: **Then** powerline glyphs, git branch icons, and box-drawing characters render sharply and in fixed monospace alignment without character distortion.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-010]` Native Windows ConPTY Environment & OSC 11 Background Color Negotiation

- **Status**: `IMPLEMENTED`
- **Type**: `Architecture`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** developer using modern TUIs (such as Neovim, Helix, or Bat) within MultiShell on Windows 11,  
> **I want** the terminal to accurately communicate its background brightness via OSC 11 and run native Windows shells in native ConPTY mode (`win32con`), so that syntax highlighting and markdown rendering display clean, accurate theme colors without background flooding or corrupted color modes.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** a terminal tab running a native Windows shell (PowerShell or CMD), **When** the shell session is initialized, **Then** `TERM` and `WT_SESSION` are NOT injected, allowing the shell and ConPTY to run in standard native `win32con` mode.
- [x] **AC-2**: **Given** a terminal tab running WSL/Linux, **When** the session is initialized, **Then** `TERM=xterm-256color` and `COLORTERM=truecolor` are provided to support Linux terminal capabilities.
- [x] **AC-3**: **Given** a running terminal session where a TUI emits an OSC 11 background query (`\x1b]11;?\x07` or `\x1b]11;?\x1b\`), **When** MultiShell receives the query stream, **Then** it responds back to the shell process input with the active theme background color in standard X11 format (`\x1b]11;rgb:0e0e/0f0f/1515\x1b\` for Dark mode or `\x1b]11;rgb:f8f8/f9f9/fcfc\x1b\` for Light mode), and strips the query before passing to the UI display model.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-TERM-011]` Smooth Terminal Rendering, PTY Output Batching & Overlay Scrollbar Anti-Flicker

- **Status**: `IMPLEMENTED`
- **Type**: `Performance`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** developer using interactive TUIs and animated CLI tools (such as `agy`, Git status/spinners, progress bars, and full-screen terminal editors) in MultiShell,  
> **I want** terminal rendering to be flicker-free and the scrollbar to float as a non-intrusive overlay without dynamically stealing character surface width, so that output streaming is silky smooth and reaching the bottom of the screen never causes infinite resize / ConPTY reflow thrashing.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an active terminal session where the shell or an interactive CLI outputs micro-chunks of text and ANSI sequences, **When** the output stream is received by MultiShell, **Then** consecutive chunks are coalesced and dispatched on the UI thread at render priority, eliminating intermediate blank/erased frames and full-screen flashing.
- [x] **AC-2**: **Given** a terminal tab whose buffer reaches and exceeds the bottom row of the viewport (`MaxScrollback > 0`), **When** the vertical scrollbar becomes visible, **Then** it renders as an overlay in Column 0 aligned to the right edge without reducing the width of the terminal character surface (`_surface`).
- [x] **AC-3**: **Then** `_surface.OnSizeChanged` does not fire merely due to the scrollbar appearing or disappearing, and no spurious `SIGWINCH` or ConPTY resize events are emitted when text scrolls into scrollback.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/TerminalTabViewModelTests.cs`

---

### `[REQ-CLI-001]` Startup Arguments & Single-Instance Tab Activation (File / Directory Path)

- **Status**: `IMPLEMENTED`
- **Type**: `Functional`
- **Target Release**: `v0.1.0`

#### User Story
> **As a** user or CLI tool,  
> **I want** to launch MultiShell with a directory or file path parameter so that if MultiShell is already running, a new tab opens in the existing window using the resolved folder and the window is brought to the foreground; and if MultiShell is not running, the application starts normally, loads its workspace state, and opens a new tab with the resolved folder.

#### Acceptance Criteria (Given-When-Then)
- [x] **AC-1**: **Given** an input argument representing an existing directory path (absolute or relative), **When** resolved, **Then** the resolver returns the normalized absolute path of the directory.
- [x] **AC-2**: **Given** an input argument representing an existing file path, **When** resolved, **Then** the resolver returns the normalized absolute path of the directory containing that file.
- [x] **AC-3**: **Given** a relative path argument and a base directory (caller's working directory), **When** resolved, **Then** the resolver computes the absolute path relative to the caller's working directory.
- [x] **AC-4**: **Given** MultiShell is already running, **When** a secondary instance is started with a valid path argument, **Then** the secondary instance forwards the resolved directory to the primary instance via Named Pipe (`MultiShell_IPC_Pipe`), the primary instance activates and opens a new tab in that directory, and the secondary instance terminates with exit code 0.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`
- **Test Suite**: `MultiShell.Tests/SingleInstanceAndStartupTests.cs`

---

### `[REQ-TERM-007]` Broadcast / Multi-Input Mode across Tabs / Panes

- **Status**: `BACKLOG`
- **Type**: `Functional`
- **Target Release**: `v1.1.0`

#### User Story
> **As a** systems operator,  
> **I want** a broadcast input toggle (`Ctrl+Shift+B`) that mirrors keyboard input simultaneously to all open tabs or split panes.

#### Acceptance Criteria (Given-When-Then)
- [ ] **AC-1**: **Given** multiple open tabs, **When** activating broadcast mode, **Then** a prominent status badge indicates broadcast is active.
- [ ] **AC-2**: **When** typing in the active terminal, **Then** identical keystrokes and escape codes are dispatched to all active PTY sessions.

#### Traceability & Verification
- **Architecture Contract**: `docs/architecture/modules/terminal-session.md`

---

## Requirements Index

| ID | Title | Type | Status | Target Release |
| :--- | :--- | :--- | :--- | :--- |
| `REQ-TAB-002` | Tab Closure & Bidirectional Process Lifecycle | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-003` | Isolated PowerShell Execution & Streaming | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-007` | True Terminal Emulation via ConPTY (PowerShell) | Architecture | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-008` | Working Directory (CWD) Tracking & Path Tab Titles | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TAB-017` | Terminal Text Selection, Copy & Paste | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-001` | Robust UTF-8 Character Streaming & Box-Drawing Rendering | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-002` | Multi-line Newline Insertion via Ctrl+Enter / Shift+Enter | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-003` | Terminal Scrollback & Buffer Control Shortcuts | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-004` | Multi-Chunk ANSI/VT100 Sequence Preservation | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-005` | Clickable Hyperlinks & Local File Paths via Ctrl+Click | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-009` | Embedded Monospace Nerd Font (FiraCode Nerd Font Mono) | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-010` | Native Windows ConPTY Environment & OSC 11 Negotiation | Architecture | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-011` | Smooth Terminal Rendering & Overlay Scrollbar Anti-Flicker | Performance | IMPLEMENTED | `v0.1.0` |
| `REQ-CLI-001` | Startup Arguments & Single-Instance Tab Activation | Functional | IMPLEMENTED | `v0.1.0` |
| `REQ-TERM-007` | Broadcast / Multi-Input Mode across Tabs / Panes | Functional | BACKLOG | `v1.1.0` |
