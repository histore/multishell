using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Service-layer abstraction representing a coordinated terminal tab that can move across windows.
/// </summary>
public interface ICoordinatedTab
{
    /// <summary>
    /// Exports the immutable persistence state for this tab.
    /// </summary>
    TabState ToTabState();
}

/// <summary>
/// Service-layer abstraction representing an application window managed by <see cref="IWindowCoordinatorService"/>.
/// </summary>
public interface ICoordinatedWindow
{
    /// <summary>
    /// Gets or sets whether this window is the primary anchor window.
    /// </summary>
    bool IsPrimary { get; set; }

    /// <summary>
    /// Gets or sets the 1-based window index.
    /// </summary>
    int WindowNumber { get; set; }

    /// <summary>
    /// Gets the number of open tabs currently hosted by this window.
    /// </summary>
    int TabCount { get; }

    /// <summary>
    /// Detaches a tab from this window without disposing its underlying shell session.
    /// </summary>
    void DetachTab(ICoordinatedTab tab);

    /// <summary>
    /// Attaches an existing tab to this window at an optional index.
    /// </summary>
    void AttachTab(ICoordinatedTab tab, int? insertIndex = null);

    /// <summary>
    /// Creates a new secondary satellite window sharing application services.
    /// </summary>
    ICoordinatedWindow CreateSatelliteWindow();

    /// <summary>
    /// Archives this window's open tabs into the closed-tab history of the specified target window.
    /// </summary>
    void ArchiveTabsTo(ICoordinatedWindow targetWindow);

    /// <summary>
    /// Pushes a closed tab snapshot into this window's recently closed tabs history.
    /// </summary>
    void PushClosedTabState(TabState closedTabState);

    /// <summary>
    /// Exports the window's workspace state for persistence.
    /// </summary>
    WindowWorkspaceState ExportWorkspaceState();

    /// <summary>
    /// Triggers workspace persistence.
    /// </summary>
    void TriggerSaveState();
}

/// <summary>
/// Event arguments for window opening requests raised by the window coordinator.
/// </summary>
public class WindowOpenRequestedEventArgs : EventArgs
{
    public ICoordinatedWindow ViewModel { get; }
    public double? ScreenX { get; }
    public double? ScreenY { get; }

    public WindowOpenRequestedEventArgs(ICoordinatedWindow viewModel, double? screenX = null, double? screenY = null)
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
    public ICoordinatedWindow ViewModel { get; }

    public WindowCloseRequestedEventArgs(ICoordinatedWindow viewModel)
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
    IReadOnlyList<ICoordinatedWindow> Windows { get; }

    /// <summary>
    /// Gets the primary anchor window, or null if no primary window is registered.
    /// </summary>
    ICoordinatedWindow? PrimaryWindow { get; }

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
    void RegisterWindow(ICoordinatedWindow viewModel, bool? isPrimary = null);

    /// <summary>
    /// Unregisters a window from the coordinator.
    /// </summary>
    void UnregisterWindow(ICoordinatedWindow viewModel);

    /// <summary>
    /// Undocks a tab from its source window into a new secondary window.
    /// </summary>
    Task<ICoordinatedWindow> UndockTabAsync(ICoordinatedWindow sourceWindow, ICoordinatedTab tab, double? screenX = null, double? screenY = null);

    /// <summary>
    /// Docks a tab back from a secondary window into the primary window.
    /// </summary>
    Task DockBackTabAsync(ICoordinatedWindow sourceWindow, ICoordinatedTab tab);

    /// <summary>
    /// Handles the closing of a window, enforcing primary vs secondary behavior.
    /// </summary>
    Task CloseWindowAsync(ICoordinatedWindow viewModel);

    /// <summary>
    /// Explicitly triggers a request to open a window in the UI (e.g. during multi-window workspace restoration).
    /// </summary>
    void TriggerWindowOpenRequested(ICoordinatedWindow viewModel, double? screenX = null, double? screenY = null);

    /// <summary>
    /// Transfers a tab between two windows at an optional target index without restarting its shell session.
    /// </summary>
    void TransferTab(ICoordinatedWindow sourceWindow, ICoordinatedWindow targetWindow, ICoordinatedTab tab, int? insertIndex = null);
}
