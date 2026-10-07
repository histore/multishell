using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using MultiShell.Models;

namespace MultiShell.Services;

public sealed partial class ShellSession : IShellSession
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
    private readonly Lock _syncRoot = new();
    private readonly Lock _inputWriteLock = new();
    private readonly string? _initialWorkingDirectory;
    private string? _lastExecutedCommand;
    private readonly ShellType _shellType;
    private readonly string? _customExecutable;
    private readonly string? _customArguments;

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
        var defaultDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(defaultDir))
        {
            defaultDir = Environment.CurrentDirectory;
        }

        _initialWorkingDirectory = !string.IsNullOrWhiteSpace(initialWorkingDirectory)
            ? initialWorkingDirectory
            : defaultDir;
        WorkingDirectory = _initialWorkingDirectory;
        Title = !string.IsNullOrWhiteSpace(title) ? title : WorkingDirectory;
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
            lock (_inputWriteLock)
            {
                if (_inputStream?.CanWrite != true) return;
                _inputStream.Write(input, 0, input.Length);
                _inputStream.Flush();
            }
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    public void Resize(int cols, int rows)
    {
        lock (_syncRoot)
        {
            cols = Math.Clamp(cols, 1, short.MaxValue);
            rows = Math.Clamp(rows, 1, short.MaxValue);
            if (_lastResize.HasValue && _lastResize.Value == (cols, rows)) return;
            _lastResize = (cols, rows);
            if (_pseudoConsole == null || _pseudoConsole.IsClosed || _pseudoConsole.IsInvalid) return;
            NativeMethods.ResizePseudoConsole(_pseudoConsole.DangerousGetHandle(), new Coord((short)cols, (short)rows));
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

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;
        IsRunning = false;
        _lifetimeCancellation?.Cancel();

        // 1. Close ConPTY handle first so that any pending Read on the output pipe breaks with EOF immediately
        _pseudoConsole?.Dispose();

        // 2. Kill the shell process tree and wait briefly for clean exit
        try
        {
            _process?.Kill(entireProcessTree: true);
            _process?.WaitForExit(500);
        }
        catch { }

        // 3. Close streams and pipe handles cleanly
        _inputStream?.Dispose();
        _outputStream?.Dispose();
        _inputWriteHandle?.Dispose();
        _outputReadHandle?.Dispose();
        _process?.Dispose();
        _lifetimeCancellation?.Dispose();
    }
}
