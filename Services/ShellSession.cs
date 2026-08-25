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

public sealed class ShellSession : IShellSession
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
    private readonly ShellType _shellType;
    private readonly string? _customExecutable;
    private readonly string? _customArguments;

    private static readonly Regex Osc9Regex = new(@"\x1b\]9;9;""?([^""\x1b\x07]+)""?(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc7Regex = new(@"\x1b\]7;file://[^/\x1b\x07]*/?([^\x1b\x07]+)(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc133ERegex = new(@"\x1b\]133;E;([^\x1b\x07]*)(\x1b\\|\x07)", RegexOptions.Compiled);
    private static readonly Regex Osc133DRegex = new(@"\x1b\]133;D;([^\x1b\x07]*)(\x1b\\|\x07)", RegexOptions.Compiled);

    public Guid SessionId { get; } = Guid.NewGuid();
    public string Title { get; }
    public string? WorkingDirectory { get; private set; }
    public ShellType ShellType => _shellType;
    public bool IsRunning { get; private set; }

    public event Action<byte[]>? DataReceived;
    public event Action<int>? Exited;
    public event Action<string>? WorkingDirectoryChanged;
    public event Action<string>? CommandExecuted;

    public ShellSession(
        string title,
        string? initialWorkingDirectory = null,
        ShellType shellType = ShellType.PowerShell,
        string? customExecutable = null,
        string? customArguments = null)
    {
        Title = title;
        _initialWorkingDirectory = string.IsNullOrWhiteSpace(initialWorkingDirectory) ? null : initialWorkingDirectory;
        WorkingDirectory = _initialWorkingDirectory;
        _shellType = shellType;
        _customExecutable = string.IsNullOrWhiteSpace(customExecutable) ? null : customExecutable;
        _customArguments = customArguments;
    }

    public void Start()
    {
        if (IsRunning) return;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("ConPTY is only available on Windows.");
        if (!NativeMethods.CreatePipePair(out var inputReadHandle, out var inputWriteHandle)) throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!NativeMethods.CreatePipePair(out var outputReadHandle, out var outputWriteHandle))
        {
            inputReadHandle.Dispose();
            inputWriteHandle.Dispose();
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        try
        {
            NativeMethods.ClearHandleInheritance(inputWriteHandle);
            NativeMethods.ClearHandleInheritance(outputReadHandle);
            var result = NativeMethods.CreatePseudoConsole(new Coord(120, 30), inputReadHandle.DangerousGetHandle(), outputWriteHandle.DangerousGetHandle(), 0, out var pseudoConsoleHandle);
            if (result != 0) Marshal.ThrowExceptionForHR(result);

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
            NativeMethods.ResizePseudoConsole(_pseudoConsole.DangerousGetHandle(), new Coord((short)cols, (short)rows));
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
                CheckForOscSequences(data);
                DataReceived?.Invoke(data);
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
        catch (OperationCanceledException) { }
    }

    private void CheckForOscSequences(byte[] data)
    {
        try
        {
            string text = Encoding.UTF8.GetString(data);
            lock (_oscBuffer)
            {
                _oscBuffer.Append(text);
                var currentBuffer = _oscBuffer.ToString();

                var matches9 = Osc9Regex.Matches(currentBuffer);
                if (matches9.Count > 0) UpdateDirectory(matches9[^1].Groups[1].Value.Trim());

                var matches7 = Osc7Regex.Matches(currentBuffer);
                if (matches7.Count > 0) UpdateDirectory(Uri.UnescapeDataString(matches7[^1].Groups[1].Value.Trim()));

                var matches133E = Osc133ERegex.Matches(currentBuffer);
                if (matches133E.Count > 0)
                {
                    var lastMatch = matches133E[^1];
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

                if (_oscBuffer.Length > 8192) _oscBuffer.Remove(0, 4096);
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
            NativeMethods.InitializeProcThreadAttributeList(attributeList, 1, 0, ref attributeListSize);
            NativeMethods.UpdateProcThreadAttribute(attributeList, 0, NativeMethods.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE, pseudoConsole.DangerousGetHandle(), (IntPtr)IntPtr.Size, IntPtr.Zero, IntPtr.Zero);

            var startupInfo = new StartupInfoEx();
            startupInfo.StartupInfo.cb = Marshal.SizeOf<StartupInfoEx>();
            startupInfo.lpAttributeList = attributeList;

            var environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables()) environmentVariables[(string)entry.Key] = (string?)entry.Value ?? string.Empty;
            environmentVariables["TERM"] = "xterm-256color";
            environmentVariables["COLORTERM"] = "truecolor";
            environmentBlock = Marshal.StringToHGlobalUni(BuildEnvironmentBlock(environmentVariables));

            string? startDir = (!string.IsNullOrEmpty(workingDir) && Directory.Exists(workingDir)) ? workingDir : null;
            string fullCommandLine = GenerateShellCommandLine(startDir);
            commandLine = Marshal.StringToHGlobalUni(fullCommandLine);

            bool success = NativeMethods.CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, NativeMethods.EXTENDED_STARTUPINFO_PRESENT | NativeMethods.CREATE_UNICODE_ENVIRONMENT, environmentBlock, startDir, ref startupInfo, out var processInformation);
            if (!success || processInformation.dwProcessId == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error, $"Failed to launch process '{fullCommandLine}'. Error code: {error}");
            }

            processHandle = new SafeFileHandle(processInformation.hProcess, ownsHandle: true);
            threadHandle = new SafeFileHandle(processInformation.hThread, ownsHandle: true);
            _process = Process.GetProcessById((int)processInformation.dwProcessId);
        }
        finally
        {
            threadHandle?.Dispose();
            processHandle?.Dispose();
            if (attributeList != IntPtr.Zero) { NativeMethods.DeleteProcThreadAttributeList(attributeList); Marshal.FreeHGlobal(attributeList); }
            if (commandLine != IntPtr.Zero) Marshal.FreeHGlobal(commandLine);
            if (environmentBlock != IntPtr.Zero) Marshal.FreeHGlobal(environmentBlock);
        }
    }

    private string GenerateShellCommandLine(string? workingDir)
    {
        if (!string.IsNullOrWhiteSpace(_customExecutable))
        {
            var args = string.IsNullOrWhiteSpace(_customArguments) ? "" : $" {_customArguments}";
            return $"\"{_customExecutable}\"{args}";
        }

        if (_shellType == ShellType.PowerShell)
        {
            string exePath = ResolveExecutable("pwsh.exe") ?? "powershell.exe";
            return $"\"{exePath}\" -NoLogo -NoExit";
        }
        else if (_shellType == ShellType.NuShell)
        {
            string exePath = ResolveExecutable("nu.exe") ?? "nu.exe";
            return $"\"{exePath}\"";
        }
        else if (_shellType == ShellType.WSL)
        {
            string exePath = ResolveExecutable("wsl.exe") ?? "wsl.exe";
            return $"\"{exePath}\"";
        }
        else
        {
            return "cmd.exe";
        }
    }

    private static string? ResolveExecutable(string name)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir, name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        if (name == "pwsh.exe")
        {
            var windir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            var winPowerShell = Path.Combine(windir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPowerShell)) return winPowerShell;
        }
        return null;
    }

    private static string BuildEnvironmentBlock(IReadOnlyDictionary<string, string> environmentVariables)
    {
        var builder = new StringBuilder();
        foreach (var entry in environmentVariables.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase))
            builder.Append(entry.Key).Append('=').Append(entry.Value).Append('\0');
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
    [StructLayout(LayoutKind.Sequential)] private struct Coord(short x, short y) { public short X = x; public short Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct SecurityAttributes { public int nLength; public IntPtr lpSecurityDescriptor; [MarshalAs(UnmanagedType.Bool)] public bool bInheritHandle; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfo { public int cb; public IntPtr lpReserved; public IntPtr lpDesktop; public IntPtr lpTitle; public int dwX; public int dwY; public int dwXSize; public int dwYSize; public int dwXCountChars; public int dwYCountChars; public int dwFillAttribute; public int dwFlags; public short wShowWindow; public short cbReserved2; public IntPtr lpReserved2; public IntPtr hStdInput; public IntPtr hStdOutput; public IntPtr hStdError; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct StartupInfoEx { public StartupInfo StartupInfo; public IntPtr lpAttributeList; }
    [StructLayout(LayoutKind.Sequential)] private struct ProcessInformation { public IntPtr hProcess; public IntPtr hThread; public int dwProcessId; public int dwThreadId; }

    private static class NativeMethods
    {
        public const int EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
        public const int CREATE_UNICODE_ENVIRONMENT = 0x00000400;
        public const int PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CreatePipe(out IntPtr hReadPipe, out IntPtr hWritePipe, IntPtr lpPipeAttributes, int nSize);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool InitializeProcThreadAttributeList(IntPtr lpAttributeList, int dwAttributeCount, int dwFlags, ref IntPtr lpSize);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool UpdateProcThreadAttribute(IntPtr lpAttributeList, uint dwFlags, IntPtr attribute, IntPtr lpValue, IntPtr cbSize, IntPtr lpPreviousValue, IntPtr lpReturnSize);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool DeleteProcThreadAttributeList(IntPtr lpAttributeList);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool CreateProcess(string? lpApplicationName, IntPtr lpCommandLine, IntPtr lpProcessAttributes, IntPtr lpThreadAttributes, bool bInheritHandles, uint dwCreationFlags, IntPtr lpEnvironment, string? lpCurrentDirectory, ref StartupInfoEx lpStartupInfo, out ProcessInformation lpProcessInformation);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern int CreatePseudoConsole(Coord size, IntPtr hConsoleInput, IntPtr hConsoleOutput, uint dwFlags, out IntPtr phPC);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern int ResizePseudoConsole(IntPtr hPC, Coord size);
        [DllImport("kernel32.dll", SetLastError = true)] internal static extern void ClosePseudoConsole(IntPtr hPC);
        [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] public static extern bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);
        [DllImport("kernel32.dll", SetLastError = true)] public static extern bool CloseHandle(IntPtr hObject);

        public static bool CreatePipePair(out SafeFileHandle readPipe, out SafeFileHandle writePipe)
        {
            if (CreatePipe(out IntPtr hRead, out IntPtr hWrite, IntPtr.Zero, 0))
            {
                readPipe = new SafeFileHandle(hRead, true);
                writePipe = new SafeFileHandle(hWrite, true);
                return true;
            }
            readPipe = new SafeFileHandle(IntPtr.Zero, true);
            writePipe = new SafeFileHandle(IntPtr.Zero, true);
            return false;
        }

        public static void ClearHandleInheritance(SafeFileHandle handle)
        {
            SetHandleInformation(handle.DangerousGetHandle(), 1, 0);
        }
    }

    private sealed class WindowsPseudoConsoleSafeHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public WindowsPseudoConsoleSafeHandle(IntPtr preExistingHandle) : base(true) { SetHandle(preExistingHandle); }
        protected override bool ReleaseHandle() { NativeMethods.ClosePseudoConsole(handle); return true; }
    }
    #endregion
}
