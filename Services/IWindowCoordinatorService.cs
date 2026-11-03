using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiShell.ViewModels;

namespace MultiShell.Services;

/// <summary>
/// Event arguments for window opening requests raised by the window coordinator.
/// </summary>
public class WindowOpenRequestedEventArgs : EventArgs
{
    public MainViewModel ViewModel { get; }
    public double? ScreenX { get; }
    public double? ScreenY { get; }

    public WindowOpenRequestedEventArgs(MainViewModel viewModel, double? screenX = null, double? screenY = null)
    {
        ViewModel = viewModel;
        ScreenX = screenX;
        ScreenY = screenY;
    }
}

/// <summary>
/// Event arguments for window close requests raised by the coordinator.
/// </summary>
public class WindowCloseRequestedEventArgs : EventArgs
{
    public MainViewModel ViewModel { get; }

    public WindowCloseRequestedEventArgs(MainViewModel viewModel)
    {
        ViewModel = viewModel;
    }
}

/// <summary>
/// Coordinates multi-window lifecycles, tab undocking, redocking, and global application shutdown.
/// </summary>
public interface IWindowCoordinatorService
{
    /// <summary>
    /// Gets all currently registered active windows.
    /// </summary>
    IReadOnlyList<MainViewModel> Windows { get; }

    /// <summary>
    /// Gets the primary anchor window, or null if no primary window is registered.
    /// </summary>
    MainViewModel? PrimaryWindow { get; }

    /// <summary>
    /// Occurs when a new window should be opened in the UI.
    /// </summary>
    event EventHandler<WindowOpenRequestedEventArgs>? WindowOpenRequested;

    /// <summary>
    /// Occurs when a specific window should be closed in the UI.
    /// </summary>
    event EventHandler<WindowCloseRequestedEventArgs>? WindowCloseRequested;

    /// <summary>
    /// Occurs when the primary window closes and all windows must terminate.
    /// </summary>
    event EventHandler? ApplicationShutdownRequested;

    /// <summary>
    /// Registers a window with the coordinator.
    /// </summary>
    void RegisterWindow(MainViewModel viewModel, bool? isPrimary = null);

    /// <summary>
    /// Unregisters a window from the coordinator.
    /// </summary>
    void UnregisterWindow(MainViewModel viewModel);

    /// <summary>
    /// Undocks a tab from its source window into a new secondary window.
    /// </summary>
    Task<MainViewModel> UndockTabAsync(MainViewModel sourceWindow, TerminalTabViewModel tab, double? screenX = null, double? screenY = null);

    /// <summary>
    /// Docks a tab back from a secondary window into the primary window.
    /// </summary>
    Task DockBackTabAsync(MainViewModel sourceWindow, TerminalTabViewModel tab);

    /// <summary>
    /// Handles the closing of a window, enforcing primary vs secondary behavior.
    /// </summary>
    Task CloseWindowAsync(MainViewModel viewModel);

    /// <summary>
    /// Explicitly triggers a request to open a window in the UI (e.g. during multi-window workspace restoration).
    /// </summary>
    void TriggerWindowOpenRequested(MainViewModel viewModel, double? screenX = null, double? screenY = null);

    /// <summary>
    /// Transfers a tab between two windows at an optional target index without restarting its shell session.
    /// </summary>
    void TransferTab(MainViewModel sourceWindow, MainViewModel targetWindow, TerminalTabViewModel tab, int? insertIndex = null);
}
