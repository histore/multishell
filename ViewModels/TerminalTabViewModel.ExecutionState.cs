using System;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MultiShell.Models;

namespace MultiShell.ViewModels;

public partial class TerminalTabViewModel
{
    private static readonly IBrush RunningActiveBrush = new SolidColorBrush(Color.Parse("#7DCFFF"));
    private static readonly IBrush RunningWaitingBrush = new SolidColorBrush(Color.Parse("#757D96"));
    private static readonly IBrush FailedStatusBrush = new SolidColorBrush(Color.Parse("#F7768E"));
    private static readonly IBrush SuccessStatusBrush = new SolidColorBrush(Color.Parse("#9ECE6A"));
    private static readonly IBrush IdleStatusBrush = new SolidColorBrush(Color.Parse("#757D96"));

    private long _commandStartTimestamp;
    private DispatcherTimer? _activityCooldownTimer;

    /// <summary>
    /// Configurable threshold duration for identifying long-running commands (default: 10 seconds per REQ-TERM-012).
    /// </summary>
    public TimeSpan LongRunningThreshold { get; set; } = TimeSpan.FromSeconds(10);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcessStatus))]
    [NotifyPropertyChangedFor(nameof(IsProcessRunning))]
    [NotifyPropertyChangedFor(nameof(IsProcessHudVisible))]
    [NotifyPropertyChangedFor(nameof(HasProcessError))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusGlyph))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusForeground))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusTooltip))]
    [NotifyPropertyChangedFor(nameof(ProcessActivityText))]
    private ProcessExecutionState _executionState = ProcessExecutionState.Idle;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcessError))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusTooltip))]
    private int? _lastExitCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusTooltip))]
    private TimeSpan _lastExecutionDuration = TimeSpan.Zero;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusGlyph))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusForeground))]
    [NotifyPropertyChangedFor(nameof(ExecutionStatusTooltip))]
    [NotifyPropertyChangedFor(nameof(ProcessActivityText))]
    private bool _isOutputActive;

    [ObservableProperty]
    private double _outputActivityOpacity = 0.65;

    [ObservableProperty]
    private string? _lastExecutedCommand;

    /// <summary>
    /// Gets whether this tab currently has an active, failed, or completed process status to display.
    /// </summary>
    public bool HasProcessStatus => ExecutionState != ProcessExecutionState.Idle;

    /// <summary>
    /// Gets whether a process/command is currently actively executing in this tab.
    /// </summary>
    public bool IsProcessRunning => ExecutionState == ProcessExecutionState.Running;

    /// <summary>
    /// Gets whether the floating in-terminal activity HUD pill should be displayed.
    /// </summary>
    public bool IsProcessHudVisible => IsProcessRunning;

    /// <summary>
    /// Gets whether the last executed process ended with a non-zero exit code.
    /// </summary>
    public bool HasProcessError => ExecutionState == ProcessExecutionState.Failed;

    /// <summary>
    /// Static glyph representing the current state: ● for actively outputting, ○ for waiting for input, ✕ for Failed, ✓ for Success.
    /// </summary>
    public string ExecutionStatusGlyph => ExecutionState switch
    {
        ProcessExecutionState.Running => IsOutputActive ? "●" : "○",
        ProcessExecutionState.Failed => "✕",
        ProcessExecutionState.Success => "✓",
        ProcessExecutionState.Idle => "●",
        _ => string.Empty
    };

    /// <summary>
    /// Foreground brush for the status indicator badge and floating HUD pill.
    /// </summary>
    public IBrush ExecutionStatusForeground => ExecutionState switch
    {
        ProcessExecutionState.Running => IsOutputActive ? RunningActiveBrush : RunningWaitingBrush,
        ProcessExecutionState.Failed => FailedStatusBrush,
        ProcessExecutionState.Success => SuccessStatusBrush,
        _ => IdleStatusBrush
    };

    /// <summary>
    /// Short localized text for the terminal status bar (e.g. "Aktiv", "Wartet auf Eingabe", "Bereit").
    /// </summary>
    public string ProcessActivityText => ExecutionState switch
    {
        ProcessExecutionState.Running => IsOutputActive
            ? Loc["Process_Activity_Active"]
            : Loc["Process_Activity_Waiting"],
        ProcessExecutionState.Failed => LastExitCode.HasValue
            ? $"{Loc["Process_Activity_Failed"]} ({LastExitCode.Value})"
            : Loc["Process_Activity_Failed"],
        ProcessExecutionState.Success => Loc["Process_Activity_Success"],
        ProcessExecutionState.Idle => Loc["Process_Activity_Ready"],
        _ => string.Empty
    };

    /// <summary>
    /// Detailed localized tooltip describing current execution state and duration.
    /// </summary>
    public string ExecutionStatusTooltip => ExecutionState switch
    {
        ProcessExecutionState.Running => IsOutputActive
            ? Loc["Process_Status_Active_Tooltip"]
            : Loc["Process_Status_Waiting_Tooltip"],
        ProcessExecutionState.Failed => string.Format(
            Loc["Process_Status_Failed_Tooltip"],
            LastExitCode ?? -1,
            LastExecutionDuration.TotalSeconds),
        ProcessExecutionState.Success => string.Format(
            Loc["Process_Status_Success_Tooltip"],
            LastExecutionDuration.TotalSeconds),
        _ => string.Empty
    };

    /// <summary>
    /// Event fired when a long-running command completes in this tab (REQ-TERM-012).
    /// </summary>
    public event Action<TerminalTabViewModel, CommandExecutionResult>? CommandExecutionCompleted;

    /// <summary>
    /// Marks the start of a command execution, transitioning state to Running and starting duration timer.
    /// </summary>
    public void MarkCommandStarted(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        LastExecutedCommand = command.Trim();
        _commandStartTimestamp = Stopwatch.GetTimestamp();
        LastExitCode = null;
        OutputActivityOpacity = 1.0;
        IsOutputActive = true;
        ExecutionState = ProcessExecutionState.Running;

        RestartCooldownTimer();
    }

    /// <summary>
    /// Triggered when the shell session reports command completion (e.g. via OSC 133;D).
    /// </summary>
    public void OnSessionCommandCompleted(int exitCode)
    {
        var duration = _commandStartTimestamp > 0
            ? Stopwatch.GetElapsedTime(_commandStartTimestamp)
            : TimeSpan.Zero;

        _commandStartTimestamp = 0;
        LastExecutionDuration = duration;
        LastExitCode = exitCode;
        ExecutionState = exitCode != 0 ? ProcessExecutionState.Failed : ProcessExecutionState.Success;
        OutputActivityOpacity = 1.0;
        IsOutputActive = false;

        _activityCooldownTimer?.Stop();

        if (duration >= LongRunningThreshold)
        {
            var result = new CommandExecutionResult(
                LastExecutedCommand ?? Title,
                exitCode,
                duration,
                ExecutionState);
            CommandExecutionCompleted?.Invoke(this, result);
        }
    }

    /// <summary>
    /// Pulses the output activity indicator when new stream bytes arrive, fading back to calm waiting state after 1.5s cooldown.
    /// </summary>
    public void PulseOutputActivity()
    {
        if (ExecutionState != ProcessExecutionState.Running) return;

        OutputActivityOpacity = 1.0;
        IsOutputActive = true;

        RestartCooldownTimer();
    }

    private void RestartCooldownTimer()
    {
        if (Avalonia.Application.Current == null) return;

        if (_activityCooldownTimer == null)
        {
            _activityCooldownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1500)
            };
            _activityCooldownTimer.Tick += (_, _) =>
            {
                _activityCooldownTimer.Stop();
                if (ExecutionState == ProcessExecutionState.Running)
                {
                    OutputActivityOpacity = 0.65;
                    IsOutputActive = false;
                }
            };
        }

        _activityCooldownTimer.Stop();
        _activityCooldownTimer.Start();
    }

    /// <summary>
    /// Simulates the 1.5-second cooldown timer expiration for deterministic unit test execution.
    /// </summary>
    internal void SimulateActivityCooldownTick()
    {
        if (ExecutionState == ProcessExecutionState.Running)
        {
            OutputActivityOpacity = 0.65;
            IsOutputActive = false;
        }
    }

    /// <summary>
    /// Resets the process status to Idle (e.g. on new user interaction or clearing).
    /// </summary>
    public void ResetProcessStatus()
    {
        _commandStartTimestamp = 0;
        _activityCooldownTimer?.Stop();
        ExecutionState = ProcessExecutionState.Idle;
        LastExitCode = null;
        OutputActivityOpacity = 0.65;
        IsOutputActive = false;
    }
}
