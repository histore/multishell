using System;
using System.Text;
using System.Threading;
using Avalonia;
using Avalonia.Threading;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class handling PTY stream processing, UI batch flushing, and shell execution status updates.
/// </summary>
public partial class TerminalPaneViewModel
{
    private void OnSessionDataReceived(ReadOnlyMemory<byte> data)
    {
        if (_isDisposed || data.IsEmpty) return;

        PulseOutputActivity();

        string? textToFeed = _ansiStreamProcessor.ProcessChunk(
            data.Span,
            onOscColorResponse: response =>
            {
                try
                {
                    Session.Send(Encoding.ASCII.GetBytes(response));
                }
                catch { }
            },
            isDarkTheme: IsDarkTerminalTheme);

        if (string.IsNullOrEmpty(textToFeed) || _isDisposed) return;

        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            if (!_isDisposed)
            {
                TerminalModel.Feed(textToFeed);
            }
        }
        else
        {
            lock (_pendingUiFeedLock)
            {
                if (_isDisposed) return;
                _pendingUiFeedBuffer.Append(textToFeed);
                if (!_isUiFeedScheduled)
                {
                    _isUiFeedScheduled = true;
                    Dispatcher.UIThread.Post(FlushPendingUiFeed, DispatcherPriority.Normal);
                }
            }
        }
    }

    private void FlushPendingUiFeed()
    {
        string text;
        lock (_pendingUiFeedLock)
        {
            _isUiFeedScheduled = false;
            if (_pendingUiFeedBuffer.Length == 0 || _isDisposed) return;
            text = _pendingUiFeedBuffer.ToString();
            _pendingUiFeedBuffer.Clear();
        }

        if (!_isDisposed)
        {
            TerminalModel.Feed(text);
        }
    }

    private void PulseOutputActivity()
    {
        if (!IsExecuting)
        {
            IsExecuting = true;
            _commandStartTime ??= DateTime.UtcNow;
            UpdateExecutionStatusBadge("● ", "Process_Activity_Active", "Active");
        }

        _silenceTimer?.Dispose();
        _silenceTimer = new Timer(_ =>
        {
            if (_isDisposed) return;
            Dispatcher.UIThread.Post(() =>
            {
                if (_isDisposed) return;
                UpdateExecutionStatusBadge("○ ", "Process_Activity_Waiting", "Waiting for input");
            });
        }, null, 1500, Timeout.Infinite);
    }

    private static void RunOnUi(Action action)
    {
        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.UIThread.Post(action);
        }
    }

    private void OnSessionExited(int exitCode)
    {
        RunOnUi(() =>
        {
            CloseRequested?.Invoke(this);
            Tab.ClosePane(this);
        });
    }

    private void OnSessionWorkingDirectoryChanged(string newDir)
    {
        if (_isDisposed) return;
        RunOnUi(() =>
        {
            WorkingDirectory = newDir;
            Title = newDir;
            DirectoryChanged?.Invoke(this, newDir);
        });
    }

    private void OnSessionCommandExecuted(string cmd)
    {
        if (_isDisposed) return;
        _commandStartTime = DateTime.UtcNow;
        RunOnUi(() =>
        {
            IsExecuting = true;
            UpdateExecutionStatusBadge("● ", "Process_Activity_Active", "Active");
        });
    }

    private void UpdateExecutionStatusBadge(string symbol, string locKey, string fallback)
    {
        var label = Tab.Loc != null ? Tab.Loc[locKey] : fallback;
        ExecutionStatusBadge = symbol + label;
        FloatingHudBadge = ExecutionStatusBadge;
    }

    private void OnSessionCommandCompleted(int exitCode)
    {
        if (_isDisposed) return;
        _silenceTimer?.Dispose();
        _silenceTimer = null;
        _commandStartTime = null;

        RunOnUi(() =>
        {
            IsExecuting = false;
            ExecutionStatusBadge = null;
            FloatingHudBadge = null;
        });
    }
}
