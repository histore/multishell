using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Data payload for active in-process tab drag operations across windows.
/// </summary>
public record TabDragTransfer(ICoordinatedWindow SourceWindow, ICoordinatedTab Tab);

/// <summary>
/// Default implementation of <see cref="IWindowCoordinatorService"/> coordinating multi-window lifecycles,
/// tab undocking/redocking, and primary anchor shutdown governance.
/// </summary>
public class WindowCoordinatorService : IWindowCoordinatorService
{
    private static readonly Lazy<WindowCoordinatorService> _lazyInstance = new(() => new WindowCoordinatorService());
    public static WindowCoordinatorService Instance => _lazyInstance.Value;

    /// <summary>
    /// Gets or sets the globally active in-process tab drag transfer.
    /// </summary>
    public static TabDragTransfer? CurrentTabDrag { get; set; }

    private readonly List<ICoordinatedWindow> _windows = new();
    private readonly Lock _lock = new();

    public IReadOnlyList<ICoordinatedWindow> Windows
    {
        get
        {
            lock (_lock)
            {
                return _windows.ToList();
            }
        }
    }

    public ICoordinatedWindow? PrimaryWindow
    {
        get
        {
            lock (_lock)
            {
                return _windows.FirstOrDefault(w => w.IsPrimary);
            }
        }
    }

    public event EventHandler<WindowOpenRequestedEventArgs>? WindowOpenRequested;
    public event EventHandler<WindowCloseRequestedEventArgs>? WindowCloseRequested;
    public event EventHandler? ApplicationShutdownRequested;

    public void RegisterWindow(ICoordinatedWindow viewModel, bool? isPrimary = null)
    {
        lock (_lock)
        {
            if (_windows.Contains(viewModel)) return;

            bool makePrimary = isPrimary ?? (_windows.Count == 0);
            if (makePrimary)
            {
                viewModel.IsPrimary = true;
                viewModel.WindowNumber = 1;
            }
            else
            {
                viewModel.IsPrimary = false;
                int nextNumber = 2;
                var usedNumbers = _windows.Select(w => w.WindowNumber).ToHashSet();
                while (usedNumbers.Contains(nextNumber))
                {
                    nextNumber++;
                }
                viewModel.WindowNumber = nextNumber;
            }

            _windows.Add(viewModel);
        }
    }

    public void UnregisterWindow(ICoordinatedWindow viewModel)
    {
        lock (_lock)
        {
            _windows.Remove(viewModel);
        }
    }

    public Task<ICoordinatedWindow> UndockTabAsync(ICoordinatedWindow sourceWindow, ICoordinatedTab tab, double? screenX = null, double? screenY = null)
    {
        // 1. Detach tab from source window without disposing its running session (Zero PTY Restart)
        sourceWindow.DetachTab(tab);

        // 2. Create secondary satellite ViewModel
        var newVm = sourceWindow.CreateSatelliteWindow();
        RegisterWindow(newVm, isPrimary: false);

        // 3. Attach tab to the new secondary window
        newVm.AttachTab(tab);

        // 4. Request UI to open new window
        WindowOpenRequested?.Invoke(this, new WindowOpenRequestedEventArgs(newVm, screenX, screenY));

        return Task.FromResult(newVm);
    }

    public Task DockBackTabAsync(ICoordinatedWindow sourceWindow, ICoordinatedTab tab)
    {
        var primary = PrimaryWindow;
        if (primary == null || primary == sourceWindow) return Task.CompletedTask;

        // 1. Detach tab from secondary source window
        sourceWindow.DetachTab(tab);

        // 2. Attach tab to primary window
        primary.AttachTab(tab);

        // 3. If source secondary window has no remaining tabs, request closing it
        if (sourceWindow.TabCount == 0)
        {
            WindowCloseRequested?.Invoke(this, new WindowCloseRequestedEventArgs(sourceWindow));
        }

        return Task.CompletedTask;
    }

    public Task CloseWindowAsync(ICoordinatedWindow viewModel)
    {
        if (viewModel.IsPrimary)
        {
            // Close all secondary satellite windows
            var secondaries = Windows.Where(w => !w.IsPrimary).ToList();
            foreach (var sec in secondaries)
            {
                WindowCloseRequested?.Invoke(this, new WindowCloseRequestedEventArgs(sec));
            }

            // Signal global application shutdown
            ApplicationShutdownRequested?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            // Secondary window closing: archive tabs into ClosedTabs of the primary window
            var primary = PrimaryWindow;
            if (primary != null)
            {
                viewModel.ArchiveTabsTo(primary);
            }

            // Request closing only this secondary window
            WindowCloseRequested?.Invoke(this, new WindowCloseRequestedEventArgs(viewModel));
        }

        return Task.CompletedTask;
    }

    public void TriggerWindowOpenRequested(ICoordinatedWindow viewModel, double? screenX = null, double? screenY = null)
    {
        WindowOpenRequested?.Invoke(this, new WindowOpenRequestedEventArgs(viewModel, screenX, screenY));
    }

    public void TransferTab(ICoordinatedWindow sourceWindow, ICoordinatedWindow targetWindow, ICoordinatedTab tab, int? insertIndex = null)
    {
        if (sourceWindow == targetWindow || tab == null)
        {
            return;
        }

        // 1. Detach from source window without disposing running session (Zero PTY Restart)
        sourceWindow.DetachTab(tab);

        // 2. Attach to target window at target index
        targetWindow.AttachTab(tab, insertIndex);

        // 3. If source was a secondary satellite window with no remaining tabs, request closing it
        if (!sourceWindow.IsPrimary && sourceWindow.TabCount == 0)
        {
            WindowCloseRequested?.Invoke(this, new WindowCloseRequestedEventArgs(sourceWindow));
        }
    }
}
