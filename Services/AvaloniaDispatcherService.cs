using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;

namespace MultiShell.Services;

/// <summary>
/// Avalonia UI implementation of <see cref="IDispatcherService"/> backed by <see cref="Dispatcher.UIThread"/>.
/// </summary>
public sealed class AvaloniaDispatcherService : IDispatcherService
{
    /// <summary>
    /// Shared singleton instance for default ViewModel construction.
    /// </summary>
    public static AvaloniaDispatcherService Instance { get; } = new();

    /// <inheritdoc />
    public bool IsAvailable => Application.Current != null;

    /// <inheritdoc />
    public bool CheckAccess() => Application.Current == null || Dispatcher.UIThread.CheckAccess();

    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Application.Current == null)
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
    }

    /// <inheritdoc />
    public void RunOnUi(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
        {
            action();
            return;
        }

        Dispatcher.UIThread.Post(action, DispatcherPriority.Normal);
    }

    /// <inheritdoc />
    public Task InvokeAsync(Func<Task> action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (CheckAccess())
        {
            return action();
        }

        return Dispatcher.UIThread.InvokeAsync(action);
    }

    /// <inheritdoc />
    public IDispatcherTimer? CreateTimer(TimeSpan interval, Action tickHandler)
    {
        ArgumentNullException.ThrowIfNull(tickHandler);

        if (Application.Current == null)
        {
            return null;
        }

        return new AvaloniaDispatcherTimerWrapper(interval, tickHandler);
    }

    private sealed class AvaloniaDispatcherTimerWrapper : IDispatcherTimer
    {
        private readonly DispatcherTimer _timer;
        private readonly EventHandler _handler;

        public AvaloniaDispatcherTimerWrapper(TimeSpan interval, Action tickHandler)
        {
            _timer = new DispatcherTimer { Interval = interval };
            _handler = (_, _) => tickHandler();
            _timer.Tick += _handler;
        }

        public void Start() => _timer.Start();

        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= _handler;
        }
    }
}
