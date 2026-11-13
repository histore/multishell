using System;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Abstraction for UI-thread marshaling and UI-bound periodic timers,
/// keeping ViewModels decoupled from UI framework dispatcher types.
/// </summary>
public interface IDispatcherService
{
    /// <summary>
    /// Gets whether an active UI dispatcher loop is available.
    /// Returns <c>false</c> in headless or unit-test environments without a UI application.
    /// </summary>
    bool IsAvailable { get; }

    /// <summary>
    /// Determines whether the calling thread has direct access to the UI thread
    /// (or whether no UI dispatcher is active).
    /// </summary>
    bool CheckAccess();

    /// <summary>
    /// Posts an action to execute asynchronously on the UI thread.
    /// </summary>
    void Post(Action action);

    /// <summary>
    /// Executes the action immediately if already on the UI thread (or when no UI dispatcher is active),
    /// otherwise posts it to the UI thread.
    /// </summary>
    void RunOnUi(Action action);

    /// <summary>
    /// Invokes an asynchronous operation on the UI thread and returns a task representing its completion.
    /// </summary>
    Task InvokeAsync(Func<Task> action);

    /// <summary>
    /// Creates a UI-thread timer with the specified interval, or <c>null</c> if no UI dispatcher is active.
    /// </summary>
    IDispatcherTimer? CreateTimer(TimeSpan interval, Action tickHandler);
}

/// <summary>
/// Represents a UI-thread periodic timer abstracted from UI framework timer implementations.
/// </summary>
public interface IDispatcherTimer : IDisposable
{
    /// <summary>
    /// Starts the timer.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the timer.
    /// </summary>
    void Stop();
}
