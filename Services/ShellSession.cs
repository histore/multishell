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
    private int _disposed;
    private volatile bool _isRunning;
    private (int cols, int rows)? _lastResize;
    private readonly Lock _syncRoot = new();
    private readonly Lock _inputWriteLock = new();
    private readonly string? _initialWorkingDirectory;
    private readonly ShellType _shellType;
    private readonly string? _customExecutable;
    private readonly string? _customArguments;

    public Guid SessionId { get; } = Guid.NewGuid();
    public string Title { get; }
    public string? WorkingDirectory { get; private set; }
    public ShellType ShellType => _shellType;
    public bool IsRunning => _isRunning;

    public event Action<ReadOnlyMemory<byte>>? DataReceived;
    public event Action<int>? Exited;
    public event Action<string>? WorkingDirectoryChanged;
    public event Action<string>? CommandExecuted;
    public event Action<int>? CommandCompleted;

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
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (_isRunning) return;
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
            // Transfer ownership of host-side pipe handles immediately so Dispose() always cleans them up on failure
            _inputWriteHandle = inputWriteHandle;
            _outputReadHandle = outputReadHandle;

            NativeMethods.ClearHandleInheritance(inputWriteHandle);
            NativeMethods.ClearHandleInheritance(outputReadHandle);
            var result = NativeMethods.CreatePseudoConsole(new Coord(120, 30), inputReadHandle.DangerousGetHandle(), outputWriteHandle.DangerousGetHandle(), 0, out var pseudoConsoleHandle);
            if (result != 0) Marshal.ThrowExceptionForHR(result);

            _pseudoConsole = new WindowsPseudoConsoleSafeHandle(pseudoConsoleHandle);

            inputReadHandle.Dispose();
            outputWriteHandle.Dispose();

            StartProcessAttachedToPseudoConsole(_pseudoConsole, _initialWorkingDirectory);

            _inputStream = new FileStream(_inputWriteHandle, FileAccess.Write, 4096, isAsync: false);
            _outputStream = new FileStream(_outputReadHandle, FileAccess.Read, 4096, isAsync: false);
            _lifetimeCancellation = new CancellationTokenSource();

            _isRunning = true;
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
        if (Volatile.Read(ref _disposed) != 0) return;

        try
        {
            lock (_inputWriteLock)
            {
                if (Volatile.Read(ref _disposed) != 0 || _inputStream?.CanWrite != true) return;
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
            if (Volatile.Read(ref _disposed) != 0) return;
            cols = Math.Clamp(cols, 1, short.MaxValue);
            rows = Math.Clamp(rows, 1, short.MaxValue);
            if (_lastResize.HasValue && _lastResize.Value == (cols, rows)) return;
            _lastResize = (cols, rows);

            var pty = _pseudoConsole;
            if (pty == null || pty.IsClosed || pty.IsInvalid) return;

            bool refAdded = false;
            try
            {
                pty.DangerousAddRef(ref refAdded);
                if (refAdded)
                {
                    NativeMethods.ResizePseudoConsole(pty.DangerousGetHandle(), new Coord((short)cols, (short)rows));
                }
            }
            catch (ObjectDisposedException)
            {
                // ConPTY handle was closed concurrently during shutdown
            }
            finally
            {
                if (refAdded)
                {
                    pty.DangerousRelease();
                }
            }
        }
    }

    private async Task WaitForExitAsync(CancellationToken ct)
    {
        var process = _process;
        if (process == null) return;
        try
        {
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            if (Volatile.Read(ref _disposed) != 0) return;
            int exitCode = process.ExitCode;
            _isRunning = false;
            Exited?.Invoke(exitCode);
        }
        catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"[ShellSession.WaitForExitAsync] Exit wait ended: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _isRunning = false;

        // 1. Detach ConPTY handle under _syncRoot so concurrent Resize() calls cannot use a freed handle
        WindowsPseudoConsoleSafeHandle? ptyToDispose;
        lock (_syncRoot)
        {
            ptyToDispose = _pseudoConsole;
            _pseudoConsole = null;
        }

        // 2. Kill the shell process tree before closing the pseudo-console or stopping pipe drainage
        //    so a noisy child process cannot fill the 4 KB output pipe and deadlock ClosePseudoConsole.
        var processToCleanup = Interlocked.Exchange(ref _process, null);
        if (processToCleanup != null)
        {
            try
            {
                processToCleanup.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException or ObjectDisposedException)
            {
                System.Diagnostics.Debug.WriteLine($"[ShellSession.Dispose] Process termination ignored: {ex.Message}");
            }
        }

        // 3. Close input stream/handle under _inputWriteLock
        lock (_inputWriteLock)
        {
            _inputStream?.Dispose();
            _inputStream = null;
            _inputWriteHandle?.Dispose();
            _inputWriteHandle = null;
        }

        // 4. Cancel lifetime token and close output read handles so conhost.exe never blocks on a full pipe buffer
        try
        {
            _lifetimeCancellation?.Cancel();
        }
        catch (ObjectDisposedException) { }

        _outputStream?.Dispose();
        _outputStream = null;
        _outputReadHandle?.Dispose();
        _outputReadHandle = null;
        _lifetimeCancellation?.Dispose();
        _lifetimeCancellation = null;

        // 5. Offload ClosePseudoConsole and brief process exit wait so closing tabs/windows never stalls the UI thread
        if (ptyToDispose != null || processToCleanup != null)
        {
            _ = Task.Run(() =>
            {
                try
                {
                    ptyToDispose?.Dispose();
                }
                catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or ObjectDisposedException)
                {
                    System.Diagnostics.Debug.WriteLine($"[ShellSession.Dispose] PseudoConsole disposal ignored: {ex.Message}");
                }

                if (processToCleanup != null)
                {
                    try
                    {
                        processToCleanup.WaitForExit(500);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException or ObjectDisposedException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ShellSession.Dispose] Background WaitForExit ignored: {ex.Message}");
                    }
                    finally
                    {
                        processToCleanup.Dispose();
                    }
                }
            });
        }
    }
}
