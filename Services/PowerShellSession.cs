using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace MultiShell.Services;

/// <summary>
/// Implements IPowerShellSession using Windows ConPTY to run PowerShell with interactive REPL and CWD tracking.
/// </summary>
public sealed class PowerShellSession : IPowerShellSession
{
    private WindowsPseudoConsoleSafeHandle? _pseudoConsole;
    private SafeFileHandle? _inputWriteHandle;
    private SafeFileHandle? _outputReadHandle;
    private FileStream? _inputStream;
    private FileStream? _outputStream;
    private Process? _process;
    private CancellationTokenSource? _lifetimeCancellation;
    private bool _isDisposed;
    private (int cols, int rows)? _lastResize;
    private readonly object _syncRoot = new();
    private readonly string? _initialWorkingDirectory;
    private readonly StringBuilder _oscBuffer = new();
    private string? _lastExecutedCommand;

    private static readonly Regex Osc9Regex = new(@"\x1b\]9;9;""?([^""\x1b\x07]+)""?(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc7Regex = new(@"\x1b\]7;file://[^/\x1b\x07]*/?([^\x1b\x07]+)(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc10Regex = new(@"\x1b\]9;10;""?([^""\x1b\x07]+)""?(\x1b\\|\x07)", RegexOptions.Compiled);

    public Guid SessionId { get; } = Guid.NewGuid();
    public string Title { get; }
    public string? WorkingDirectory { get; private set; }
    public bool IsRunning { get; private set; }

    public event Action<byte[]>? DataReceived;
    public event Action<int>? Exited;
    public event Action<string>? WorkingDirectoryChanged;
    public event Action<string>? CommandExecuted;

    public PowerShellSession(string title, string? initialWorkingDirectory = null)
    {
        Title = title;
        _initialWorkingDirectory = string.IsNullOrWhiteSpace(initialWorkingDirectory) ? null : initialWorkingDirectory;
        WorkingDirectory = _initialWorkingDirectory;
    }

    public void Start()
    {
        if (IsRunning) return;

        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("ConPTY is only available on Windows.");
        }

        if (!NativeMethods.CreatePipePair(out var inputReadHandle, out var inputWriteHandle))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create ConPTY input pipe.");
        }

        if (!NativeMethods.CreatePipePair(out var outputReadHandle, out var outputWriteHandle))
        {
            inputReadHandle.Dispose();
            inputWriteHandle.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create ConPTY output pipe.");
        }

        try
        {
            NativeMethods.ClearHandleInheritance(inputWriteHandle);
            NativeMethods.ClearHandleInheritance(outputReadHandle);

            var result = NativeMethods.CreatePseudoConsole(
                new Coord(120, 30),
                inputReadHandle.DangerousGetHandle(),
                outputWriteHandle.DangerousGetHandle(),
                0,
                out var pseudoConsoleHandle);

            if (result != 0)
            {
                Marshal.ThrowExceptionForHR(result);
            }

            _pseudoConsole = new WindowsPseudoConsoleSafeHandle(pseudoConsoleHandle);
            _inputWriteHandle = inputWriteHandle;
            _outputReadHandle = outputReadHandle;

            inputReadHandle.Dispose();
            outputWriteHandle.Dispose();

            StartProcessAttachedToPseudoConsole(_pseudoConsole, _initialWorkingDirectory);

            _inputStream = new FileStream(_inputWriteHandle, FileAccess.Write, 4096, isAsync: false);
            _outputStream = new FileStream(_outputReadHandle, FileAccess.Read, 4096, isAsync: false);
            _lifetimeCancellation = new CancellationTokenSource();

            IsRunning = true;

            _ = Task.Run(() => PumpOutput(_outputStream, _lifetimeCancellation.Token));
            _ = WaitForExitAsync(_lifetimeCancellation.Token);
        }
        catch
        {
            inputReadHandle.Dispose();
            outputWriteHandle.Dispose();
            Dispose();
            throw;
        }
    }

    public void Send(byte[] input)
    {
        try
        {
            if (_inputStream?.CanWrite != true) return;
            _inputStream.Write(input, 0, input.Length);
            _inputStream.Flush();
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public void Resize(int cols, int rows)
    {
        lock (_syncRoot)
        {
            cols = Math.Max(cols, 1);
            rows = Math.Max(rows, 1);

            if (_lastResize.HasValue && _lastResize.Value == (cols, rows)) return;

            _lastResize = (cols, rows);

            if (_pseudoConsole == null || _pseudoConsole.IsClosed || _pseudoConsole.IsInvalid) return;

            var result = NativeMethods.ResizePseudoConsole(_pseudoConsole.DangerousGetHandle(), new Coord((short)cols, (short)rows));
            if (result != 0)
            {
                Debug.WriteLine($"ResizePseudoConsole failed: 0x{result:X8}");
            }
        }
    }

    private void PumpOutput(Stream stream, CancellationToken ct)
    {
        byte[] buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;

                var data = buffer.AsSpan(0, bytesRead).ToArray();

                // Scan for OSC 9;9 or OSC 7 working directory escape sequences
                CheckForWorkingDirectoryUpdate(data);

                DataReceived?.Invoke(data);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
    }

    private void CheckForWorkingDirectoryUpdate(byte[] data)
    {
        try
        {
            string text = Encoding.UTF8.GetString(data);
            lock (_oscBuffer)
            {
                _oscBuffer.Append(text);
                var currentBuffer = _oscBuffer.ToString();

                // Check OSC 9;9 matches
                var matches9 = Osc9Regex.Matches(currentBuffer);
                if (matches9.Count > 0)
                {
                    var lastMatch = matches9[^1];
                    string path = lastMatch.Groups[1].Value.Trim();
                    UpdateDirectory(path);
                }

                // Check OSC 7 matches
                var matches7 = Osc7Regex.Matches(currentBuffer);
                if (matches7.Count > 0)
                {
                    var lastMatch = matches7[^1];
                    string path = Uri.UnescapeDataString(lastMatch.Groups[1].Value.Trim());
                    UpdateDirectory(path);
                }

                // Check OSC 9;10 command execution matches
                var matches10 = Osc10Regex.Matches(currentBuffer);
                if (matches10.Count > 0)
                {
                    var lastMatch = matches10[^1];
                    string base64 = lastMatch.Groups[1].Value.Trim();
                    try
                    {
                        var bytes = Convert.FromBase64String(base64);
                        var cmd = Encoding.UTF8.GetString(bytes).Trim();
                        if (!string.IsNullOrWhiteSpace(cmd) && !string.Equals(_lastExecutedCommand, cmd, StringComparison.Ordinal))
                        {
                            _lastExecutedCommand = cmd;
                            CommandExecuted?.Invoke(cmd);
                        }
                    }
                    catch { }
                }

                if (_oscBuffer.Length > 8192)
                {
                    _oscBuffer.Remove(0, 4096);
                }
            }
        }
        catch { }
    }

    private void UpdateDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path) && !string.Equals(WorkingDirectory, path, StringComparison.OrdinalIgnoreCase))
        {
            WorkingDirectory = path;
            WorkingDirectoryChanged?.Invoke(path);
        }
    }

    private async Task WaitForExitAsync(CancellationToken ct)
    {
        if (_process == null) return;

        try
        {
            await _process.WaitForExitAsync(ct).ConfigureAwait(false);
            int exitCode = _process.ExitCode;
            IsRunning = false;
            Exited?.Invoke(exitCode);
        }
        catch (OperationCanceledException) { }
    }

    private void StartProcessAttachedToPseudoConsole(WindowsPseudoConsoleSafeHandle pseudoConsole, string? workingDir)
    {
        IntPtr attributeList = IntPtr.Zero;
        IntPtr commandLine = IntPtr.Zero;
        IntPtr environmentBlock = IntPtr.Zero;
        SafeFileHandle? processHandle = null;
        SafeFileHandle? threadHandle = null;

        try
        {
            var attributeListSize = IntPtr.Zero;
            NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attributeListSize);

            attributeList = Marshal.AllocHGlobal(attributeListSize);
            if (!NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to initialize process attribute list.");
            }

            if (!NativeMethods.UpdateProcThreadAttribute(
                    attributeList,
                    0,
                    NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pseudoConsole.DangerousGetHandle(),
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero,
                    IntPtr.Zero))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to attach pseudo console attribute.");
            }

            var startupInfo = new StartupInfoEx();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            startupInfo.lpAttributeList = attributeList;

            string exePath = ResolvePowerShellExecutable();

            // Setup prompt hook to emit OSC 9;9 (cwd) and OSC 9;10 (base64 command) on each prompt rendering
            string cdScript = !string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir)
                ? $"Set-Location -LiteralPath '{workingDir.Replace("'", "''")}'; "
                : string.Empty;
            string promptScript = "function prompt { $p = $ExecutionContext.SessionState.Path.CurrentLocation.Path; [Console]::Write([char]27 + ']9;9;\"' + $p + '\"' + [char]27 + '\\'); $last = (Get-History -Count 1 | Select-Object -ExpandProperty CommandLine -ErrorAction SilentlyContinue); if ($last) { $b = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($last)); [Console]::Write([char]27 + ']9;10;\"' + $b + '\"' + [char]27 + '\\'); }; 'PS ' + $p + '> ' }";
            string fullCommandLine = $"\"{exePath}\" -NoLogo -NoExit -Command \"{cdScript}{promptScript}; Clear-History\"";

            commandLine = Marshal.StringToHGlobalUni(fullCommandLine);

            var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                environmentVariables[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            }

            environmentVariables["TERM"] = "xterm-256color";
            environmentVariables["COLORTERM"] = "truecolor";
            environmentBlock = Marshal.StringToHGlobalUni(BuildEnvironmentBlock(environmentVariables));

            string? startDir = null;
            if (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir))
            {
                startDir = workingDir;
            }

            if (!NativeMethods.CreateProcess(
                    lpApplicationName: null,
                    lpCommandLine: commandLine,
                    lpProcessAttributes: IntPtr.Zero,
                    lpThreadAttributes: IntPtr.Zero,
                    bInheritHandles: false,
                    dwCreationFlags: NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT,
                    lpEnvironment: environmentBlock,
                    lpCurrentDirectory: startDir,
                    lpStartupInfo: ref startupInfo,
                    lpProcessInformation: out var processInformation))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to start PowerShell attached to ConPTY.");
            }

            processHandle = new SafeFileHandle(processInformation.hProcess, ownsHandle: true);
            threadHandle = new SafeFileHandle(processInformation.hThread, ownsHandle: true);

            _process = Process.GetProcessById((int)processInformation.dwProcessId);
        }
        finally
        {
            threadHandle?.Dispose();
            processHandle?.Dispose();

            if (attributeList != IntPtr.Zero)
            {
                NativeMethods.DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            if (commandLine != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(commandLine);
            }

            if (environmentBlock != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(environmentBlock);
            }
        }
    }

    private static string ResolvePowerShellExecutable()
    {
        // Try pwsh.exe first (PowerShell 7+)
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(dir, "pwsh.exe");
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        // Fallback to Windows PowerShell
        var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var winPowerShell = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        if (File.Exists(winPowerShell))
        {
            return winPowerShell;
        }

        return "powershell.exe";
    }

    private static string BuildEnvironmentBlock(IReadOnlyDictionary<string, string> environmentVariables)
    {
        var builder = new StringBuilder();
        foreach (var entry in environmentVariables.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            builder.Append(entry.Key)
                .Append('=')
                .Append(entry.Value)
                .Append('\0');
        }

        builder.Append('\0');
        return builder.ToString();
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsRunning = false;

        _lifetimeCancellation?.Cancel();

        try { _process?.Kill(entireProcessTree: true); } catch { }

        _inputStream?.Dispose();
        _outputStream?.Dispose();
        _pseudoConsole?.Dispose();
        _inputWriteHandle?.Dispose();
        _outputReadHandle?.Dispose();
        _process?.Dispose();
        _lifetimeCancellation?.Dispose();
    }

    #region Win32 ConPTY Interop

    [StructLayout(LayoutKind.Sequential)]
    private struct Coord(short x, short y)
    {
        public short X = x;
        public short Y = y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int nLength;
        public IntPtr lpSecurityDescriptor;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bInheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx
    {
        public StartupInfo StartupInfo;
        public IntPtr lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    private sealed class WindowsPseudoConsoleSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WindowsPseudoConsoleSafeHandle() : base(ownsHandle: true) { }
        public WindowsPseudoConsoleSafeHandle(IntPtr preexistingHandle, bool ownsHandle = true) : base(ownsHandle)
        {
            SetHandle(preexistingHandle);
        }

        protected override bool ReleaseHandle()
        {
            NativeMethods.ClosePseudoConsole(handle);
            return true;
        }
    }

    private static class NativeMethods
    {
        internal const int HANDLE_FLAG_INHERIT = 0x00000001;
        internal const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        internal const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        internal static readonly IntPtr PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = (IntPtr)0x00020016;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CreatePipe(
            out SafeFileHandle hReadPipe,
            out SafeFileHandle hWritePipe,
            ref SecurityAttributes lpPipeAttributes,
            int nSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool SetHandleInformation(SafeHandle hObject, int dwMask, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int CreatePseudoConsole(
            Coord size,
            IntPtr hInput,
            IntPtr hOutput,
            uint dwFlags,
            out IntPtr phPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern int ResizePseudoConsole(IntPtr hPC, Coord size);

        [DllImport("kernel32.dll")]
        internal static extern void ClosePseudoConsole(IntPtr hPC);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool InitializeProcThreadAttributeList(
            IntPtr lpAttributeList,
            int dwAttributeCount,
            int dwFlags,
            ref IntPtr lpSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool UpdateProcThreadAttribute(
            IntPtr lpAttributeList,
            uint dwFlags,
            IntPtr attribute,
            IntPtr lpValue,
            IntPtr cbSize,
            IntPtr lpPreviousValue,
            IntPtr lpReturnSize);

        [DllImport("kernel32.dll")]
        internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern bool CreateProcess(
            string? lpApplicationName,
            IntPtr lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
            int dwCreationFlags,
            IntPtr lpEnvironment,
            string? lpCurrentDirectory,
            ref StartupInfoEx lpStartupInfo,
            out ProcessInformation lpProcessInformation);

        internal static bool CreatePipePair(out SafeFileHandle readPipe, out SafeFileHandle writePipe)
        {
            var attributes = new SecurityAttributes
            {
                nLength = Marshal.SizeOf<SecurityAttributes>(),
                bInheritHandle = true,
            };

            return CreatePipe(out readPipe, out writePipe, ref attributes, 0);
        }

        internal static void ClearHandleInheritance(SafeHandle handle)
        {
            if (!SetHandleInformation(handle, HANDLE_FLAG_INHERIT, 0))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to clear handle inheritance.");
            }
        }
    }

    #endregion
}
