using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Manages application single-instance enforcement using a system Mutex
/// and cross-process argument forwarding via asynchronous Named Pipes.
/// </summary>
public class SingleInstanceService : ISingleInstanceService
{
    public const string DefaultMutexName = @"Local\MultiShell_SingleInstance_Mutex";
    public const string DefaultPipeNamePrefix = "MultiShell_IPC_Pipe";

    public static string DefaultPipeName => $"{DefaultPipeNamePrefix}_{Environment.UserName.Replace('\\', '_')}";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly IStartupPathResolver _pathResolver;
    private readonly Mutex? _mutex;
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _pipeLock = new();
    private NamedPipeServerStream? _currentServer;
    private Task? _serverTask;
    private bool _isDisposed;

    public bool IsFirstInstance { get; }

    public event Action<string?>? DirectoryOpenRequested;

    public SingleInstanceService(
        string? mutexName = null,
        string? pipeName = null,
        IStartupPathResolver? pathResolver = null)
    {
        _mutexName = !string.IsNullOrWhiteSpace(mutexName) ? mutexName : DefaultMutexName;
        _pipeName = !string.IsNullOrWhiteSpace(pipeName) ? pipeName : DefaultPipeName;
        _pathResolver = pathResolver ?? new StartupPathResolver();

        try
        {
            _mutex = new Mutex(true, _mutexName, out bool createdNew);
            IsFirstInstance = createdNew;
        }
        catch
        {
            // If mutex creation fails (e.g. access denied), treat as secondary instance
            IsFirstInstance = false;
        }
    }

    public void StartServer()
    {
        if (!IsFirstInstance || _serverTask != null || _isDisposed)
        {
            return;
        }

        _serverTask = Task.Run(RunServerLoopAsync);
    }

    private async Task RunServerLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            if (!await AcceptNextConnectionAsync().ConfigureAwait(false))
            {
                break;
            }
        }
    }

    private async Task<bool> AcceptNextConnectionAsync()
    {
        NamedPipeServerStream? server = null;
        try
        {
            server = new NamedPipeServerStream(
                _pipeName,
                PipeDirection.In,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);

            if (!TrySetCurrentServer(server))
            {
                server.Dispose();
                return false;
            }

            await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);
            var targetDirectory = await ReadTargetDirectoryAsync(server).ConfigureAwait(false);
            DirectoryOpenRequested?.Invoke(targetDirectory);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
        catch
        {
            return await DelayAfterPipeErrorAsync().ConfigureAwait(false);
        }
        finally
        {
            ClearCurrentServer(server);
            server?.Dispose();
        }
    }

    private bool TrySetCurrentServer(NamedPipeServerStream server)
    {
        lock (_pipeLock)
        {
            if (_isDisposed || _cts.IsCancellationRequested) return false;
            _currentServer = server;
            return true;
        }
    }

    private void ClearCurrentServer(NamedPipeServerStream? server)
    {
        lock (_pipeLock)
        {
            if (_currentServer == server)
            {
                _currentServer = null;
            }
        }
    }

    private async Task<string?> ReadTargetDirectoryAsync(NamedPipeServerStream server)
    {
        using var readCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        readCts.CancelAfter(TimeSpan.FromSeconds(5));

        using var reader = new StreamReader(server, Encoding.UTF8);
        var buffer = new char[4096];
        int charsRead = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), readCts.Token).ConfigureAwait(false);
        var message = new string(buffer, 0, charsRead);

        return string.IsNullOrWhiteSpace(message)
            ? null
            : _pathResolver.ResolveWorkingDirectory(message.Trim());
    }

    private async Task<bool> DelayAfterPipeErrorAsync()
    {
        if (_cts.IsCancellationRequested) return false;
        try
        {
            await Task.Delay(100, _cts.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    public async Task<bool> SendArgsToFirstInstanceAsync(string[] args, string clientCurrentDirectory, int timeoutMs = 2000)
    {
        var resolvedPath = _pathResolver.ResolveFromArgs(args, clientCurrentDirectory);
        int primaryTimeout = _pipeName != DefaultPipeNamePrefix ? Math.Max(250, timeoutMs / 2) : timeoutMs;

        if (await TrySendToPipeAsync(_pipeName, resolvedPath, primaryTimeout).ConfigureAwait(false))
        {
            return true;
        }

        // Backward compatibility fallback: if user-isolated pipe connection fails, try legacy pipe
        if (_pipeName != DefaultPipeNamePrefix)
        {
            int fallbackTimeout = Math.Max(250, timeoutMs - primaryTimeout);
            return await TrySendToPipeAsync(DefaultPipeNamePrefix, resolvedPath, fallbackTimeout).ConfigureAwait(false);
        }

        return false;
    }

    private static async Task<bool> TrySendToPipeAsync(string pipeName, string? message, int timeoutMs)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
            await client.ConnectAsync(timeoutMs).ConfigureAwait(false);

            using var writer = new StreamWriter(client, Encoding.UTF8);
            await writer.WriteAsync(message ?? string.Empty).ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        try
        {
            _cts.Cancel();
        }
        catch
        {
            // Ignore cancellation disposal errors
        }

        lock (_pipeLock)
        {
            try
            {
                _currentServer?.Dispose();
                _currentServer = null;
            }
            catch
            {
                // Ignore pipe disposal errors
            }
        }

        if (IsFirstInstance && _mutex != null)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch
            {
                // Mutex might not be held or already abandoned
            }
        }

        _mutex?.Dispose();
        _cts.Dispose();
    }
}
