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
    public const string DefaultPipeName = "MultiShell_IPC_Pipe";

    private readonly string _mutexName;
    private readonly string _pipeName;
    private readonly IStartupPathResolver _pathResolver;
    private readonly Mutex? _mutex;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _pipeLock = new();
    private NamedPipeServerStream? _currentServer;
    private Task? _serverTask;
    private bool _isDisposed;

    public bool IsFirstInstance { get; }

    public event Action<string?>? DirectoryOpenRequested;

    public SingleInstanceService(
        string mutexName = DefaultMutexName,
        string pipeName = DefaultPipeName,
        IStartupPathResolver? pathResolver = null)
    {
        _mutexName = mutexName;
        _pipeName = pipeName;
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

        _serverTask = Task.Run(async () =>
        {
            while (!_cts.IsCancellationRequested)
            {
                NamedPipeServerStream? server = null;
                try
                {
                    server = new NamedPipeServerStream(
                        _pipeName,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    lock (_pipeLock)
                    {
                        if (_isDisposed || _cts.IsCancellationRequested)
                        {
                            server.Dispose();
                            break;
                        }
                        _currentServer = server;
                    }

                    await server.WaitForConnectionAsync(_cts.Token).ConfigureAwait(false);

                    using var reader = new StreamReader(server, Encoding.UTF8);
                    var message = await reader.ReadToEndAsync(_cts.Token).ConfigureAwait(false);

                    var targetDirectory = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
                    DirectoryOpenRequested?.Invoke(targetDirectory);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch
                {
                    if (_cts.IsCancellationRequested) break;
                    // Short back-off on transient pipe error before listening again
                    try
                    {
                        await Task.Delay(100, _cts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
                finally
                {
                    lock (_pipeLock)
                    {
                        if (_currentServer == server)
                        {
                            _currentServer = null;
                        }
                    }
                    server?.Dispose();
                }
            }
        });
    }

    public async Task<bool> SendArgsToFirstInstanceAsync(string[] args, string clientCurrentDirectory, int timeoutMs = 2000)
    {
        var resolvedPath = _pathResolver.ResolveFromArgs(args, clientCurrentDirectory);

        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            await client.ConnectAsync(timeoutMs).ConfigureAwait(false);

            using var writer = new StreamWriter(client, Encoding.UTF8);
            await writer.WriteAsync(resolvedPath ?? string.Empty).ConfigureAwait(false);
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
