using System;
using System.IO;
using System.Text;
using System.Threading;

namespace MultiShell.Services;

/// <summary>
/// Coordinates ANSI stream processing, OSC color query responses, and batched UI-thread terminal feeding
/// shared across tab and split-pane ViewModels (BL-QUAL-003, REQ-TERM-011).
/// </summary>
public sealed class TerminalUiFeedCoordinator : IDisposable
{
    private readonly AnsiStreamProcessor _ansiStreamProcessor = new();
    private readonly StringBuilder _pendingUiFeedBuffer;
    private readonly Lock _pendingUiFeedLock = new();
    private bool _isUiFeedScheduled;
    private bool _isDisposed;

    public TerminalUiFeedCoordinator(StringBuilder? pendingBuffer = null)
    {
        _pendingUiFeedBuffer = pendingBuffer ?? new StringBuilder();
    }

    public static void DispatchOscColorQueries(string text, bool isDarkTheme, IShellSession session)
    {
        AnsiStreamProcessor.DispatchOscColorQueries(text, isDarkTheme, response =>
        {
            TrySendOscColorResponse(session, response);
        });
    }

    public void ProcessAndFeed(
        ReadOnlySpan<byte> data,
        bool isDarkTheme,
        IShellSession session,
        ITerminalModelAdapter terminalModel,
        IDispatcherService dispatcherService)
    {
        if (_isDisposed || data.IsEmpty) return;

        string? textToFeed = _ansiStreamProcessor.ProcessChunk(
            data,
            onOscColorResponse: response => TrySendOscColorResponse(session, response),
            isDarkTheme: isDarkTheme);

        if (string.IsNullOrEmpty(textToFeed) || _isDisposed) return;

        if (dispatcherService.CheckAccess())
        {
            if (!_isDisposed)
            {
                terminalModel.Feed(textToFeed);
            }
        }
        else
        {
            // Coalesce rapid PTY chunks onto the UI thread so background tabs process output reliably without starving (REQ-TERM-011)
            lock (_pendingUiFeedLock)
            {
                if (_isDisposed) return;
                _pendingUiFeedBuffer.Append(textToFeed);
                if (!_isUiFeedScheduled)
                {
                    _isUiFeedScheduled = true;
                    dispatcherService.Post(() => Flush(terminalModel));
                }
            }
        }
    }

    public void Flush(ITerminalModelAdapter terminalModel)
    {
        if (_isDisposed) return;

        string batchText;
        lock (_pendingUiFeedLock)
        {
            _isUiFeedScheduled = false;
            if (_isDisposed || _pendingUiFeedBuffer.Length == 0) return;

            batchText = _pendingUiFeedBuffer.ToString();
            _pendingUiFeedBuffer.Clear();

            // Prevent StringBuilder from holding huge memory capacity permanently after massive output bursts
            if (_pendingUiFeedBuffer.Capacity > 65536)
            {
                _pendingUiFeedBuffer.Capacity = 4096;
            }
        }

        if (!_isDisposed)
        {
            terminalModel.Feed(batchText);
        }
    }

    private static void TrySendOscColorResponse(IShellSession session, string response)
    {
        try
        {
            session.Send(Encoding.ASCII.GetBytes(response));
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalUiFeedCoordinator] OSC color response send ignored: {ex.Message}");
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        lock (_pendingUiFeedLock)
        {
            _pendingUiFeedBuffer.Clear();
            _pendingUiFeedBuffer.Capacity = 0;
            _isUiFeedScheduled = false;
        }
    }
}
