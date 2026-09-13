# Terminal Session & ConPTY Engine

## 1. Overview & Purpose
The Terminal Session module encapsulates low-level operating system process management, Win32 PseudoConsole (ConPTY) virtualization, asynchronous I/O streaming, stateful UTF-8 decoding, and terminal escape sequence parsing. It isolates the rest of the application from native P/Invoke handles and external shell processes.

## 2. Core Components & Contracts

### 2.1 Contracts (`Services/`)
* **[`IShellSession`](../../../Services/IShellSession.cs)**:
  * Public abstraction exposing stream events (`DataReceived`, `ProcessExited`, `WorkingDirectoryChanged`, `CommandExecuted`).
  * Provides control APIs: `Write(byte[] data)`, `Resize(int columns, int rows)`, and `Terminate()`.
* **[`IShellProcessService`](../../../Services/IShellProcessService.cs)**:
  * Factory interface for instantiating concrete `IShellSession` sessions based on requested profiles and working directories.
* **[`IShellDiscoveryService`](../../../Services/IShellDiscoveryService.cs)**:
  * Enumerates available shell executables across the host environment.

### 2.2 Implementations
* **[`ShellSession`](../../../Services/ShellSession.cs)**:
  * Manages ConPTY lifecycle via native `CreatePseudoConsole`, `ResizePseudoConsole`, and `ClosePseudoConsole` P/Invoke calls.
  * Connects standard input/output anonymous pipes between ConPTY and the parent process.
  * Spawns worker processes using `STARTUPINFOEX` and `PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE`.
  * Runs background async reading loops on the stdout pipe.
* **[`ShellProcessService`](../../../Services/ShellProcessService.cs)**:
  * Concrete factory constructing `ShellSession` instances with initial buffer dimensions and environment configurations.
* **[`ShellDiscoveryService`](../../../Services/ShellDiscoveryService.cs)**:
  * Auto-discovers PowerShell 7 (`pwsh.exe`), Windows PowerShell (`powershell.exe`), Command Prompt (`cmd.exe`), WSL distributions (`wsl.exe`), and NuShell (`nu.exe`).

## 3. Data Flow & Streaming Sequence

```mermaid
sequenceDiagram
    participant UI as TerminalTabView (Avalonia)
    participant VM as TerminalTabViewModel
    participant SS as ShellSession (ConPTY)
    participant Pipe as Named/Anonymous Pipe
    participant Shell as pwsh.exe / wsl.exe

    UI->>VM: User Input (Key / Paste Bytes)
    VM->>SS: Send(byte[] data)
    SS->>Pipe: Write to InPipe (stdin)
    Pipe->>Shell: Shell Input Buffer

    Shell->>Pipe: Shell Output Buffer (VT/ANSI/OSC)
    Pipe->>SS: Read from OutPipe (stdout)
    SS->>SS: CheckForOscSequences (OSC 7 / 9;9 / 133;E)
    opt OSC Sequence Detected
        SS-->>VM: WorkingDirectoryChanged / CommandExecuted
    end
    SS->>VM: DataReceived (raw chunk bytes)
    VM->>VM: Stateful UTF-8 Decoder (Decoder.GetChars)
    VM->>UI: TerminalModel.Feed(decodedText)
```

## 4. Key Implementation Details & Invariants

### 4.1 Stateful UTF-8 Decoding
ConPTY streaming delivers arbitrary byte buffers over anonymous pipes. Multi-byte UTF-8 sequences (such as emoji or accented characters) may be split across buffer boundaries. `ShellSession` and `TerminalTabViewModel` use persistent `System.Text.Decoder` instances across reads rather than `Encoding.UTF8.GetString(...)` to prevent replacement characters (`U+FFFD`) or character corruption.

### 4.2 Shell Integration (OSC Sequences)
* **OSC 9;9 (`\x1b]9;9;"<path>"\x07`)**: Working directory notification emitted by PowerShell profile hooks.
* **OSC 7 (`\x1b]7;file://<host>/<path>\x07`)**: Standard POSIX/WSL working directory escape sequence.
* **OSC 133;E (`\x1b]133;E;<base64-command>\x07`)**: Execution notification capturing the exact command line executed by the shell.

### 4.3 Safe Disposal & Leak-Free Teardown
On tab closure or application exit:
1. ConPTY pseudo console is released via `ClosePseudoConsole`.
2. Asynchronous pipe cancellation tokens are triggered.
3. Pipe stream handles (`SafeFileHandle`) are explicitly disposed.
4. The underlying process and any child processes are terminated cleanly to avoid orphan background processes.
