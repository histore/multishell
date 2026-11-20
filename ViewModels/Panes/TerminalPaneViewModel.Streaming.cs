using System;
using System.Text;
using System.Threading;

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
        if (IsActive)
        {
            Tab.PulseOutputActivity();
        }
        _uiFeedCoordinator.ProcessAndFeed(data.Span, IsDarkTerminalTheme, Session, TerminalModel, _dispatcherService);
    }

    private void PulseOutputActivity()
    {
        if (_isDisposed) return;

        _dispatcherService.RunOnUi(() =>
        {
            if (_isDisposed) return;
            PaneIsOutputActive = true;
            if (!IsExecuting)
            {
                IsExecuting = true;
                _commandStartTime ??= DateTime.UtcNow;
                UpdateExecutionStatusBadge("● ", "Process_Activity_Active", "Active");
            }
        });

        lock (_silenceTimerLock)
        {
            if (_isDisposed) return;
            if (_silenceTimer == null)
            {
                _silenceTimer = new Timer(OnSilenceTimerElapsed, null, 1500, Timeout.Infinite);
            }
            else
            {
                _silenceTimer.Change(1500, Timeout.Infinite);
            }
        }
    }

    private void OnSilenceTimerElapsed(object? _)
    {
        if (_isDisposed) return;
        _dispatcherService.RunOnUi(() =>
        {
            if (_isDisposed) return;
            PaneIsOutputActive = false;
            UpdateExecutionStatusBadge("○ ", "Process_Activity_Waiting", "Waiting for input");
        });
    }

    private void OnSessionExited(int exitCode)
    {
        if (_isDisposed) return;
        Tab.HandlePaneSessionExited(this, exitCode);
    }

    private void OnSessionWorkingDirectoryChanged(string newDir)
    {
        if (_isDisposed) return;
        _dispatcherService.RunOnUi(() =>
        {
            if (_isDisposed) return;
            WorkingDirectory = newDir;
            Title = newDir;
            DirectoryChanged?.Invoke(this, newDir);
        });

        if (!ReferenceEquals(Tab.TerminalModel, TerminalModel))
        {
            Tab.HandlePaneWorkingDirectoryChanged(this, newDir);
        }
    }

    private void OnSessionCommandExecuted(string cmd)
    {
        if (_isDisposed) return;
        _commandStartTime = DateTime.UtcNow;
        _dispatcherService.RunOnUi(() =>
        {
            if (_isDisposed) return;
            IsExecuting = true;
            UpdateExecutionStatusBadge("● ", "Process_Activity_Active", "Active");
        });

        if (!ReferenceEquals(Tab.TerminalModel, TerminalModel))
        {
            Tab.HandlePaneCommandExecuted(this, cmd);
        }
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

        lock (_silenceTimerLock)
        {
            _silenceTimer?.Dispose();
            _silenceTimer = null;
        }
        _commandStartTime = null;

        _dispatcherService.RunOnUi(() =>
        {
            if (_isDisposed) return;
            IsExecuting = false;
            ExecutionStatusBadge = null;
            FloatingHudBadge = null;
        });

        if (!ReferenceEquals(Tab.TerminalModel, TerminalModel))
        {
            Tab.HandlePaneCommandCompleted(this, exitCode);
        }
    }
}
