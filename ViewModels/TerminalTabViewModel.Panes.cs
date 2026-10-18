using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

public partial class TerminalTabViewModel
{
    private readonly ObservableCollection<TerminalPaneViewModel> _panes = new();

    /// <summary>
    /// Gets the collection of all terminal panes currently open in this tab.
    /// </summary>
    public ReadOnlyObservableCollection<TerminalPaneViewModel> Panes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayRootNode))]
    private IPaneNodeViewModel _rootPaneNode = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayRootNode))]
    private TerminalPaneViewModel _activePane = null!;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayRootNode))]
    [NotifyPropertyChangedFor(nameof(DisplayTitle))]
    private bool _isZoomed;

    /// <summary>
    /// Raised whenever the split pane tree structure, count, or zoom state changes (REQ-UI-006).
    /// </summary>
    public event Action? LayoutChanged;

    /// <summary>
    /// Gets whether this tab currently contains more than one pane (REQ-UI-006).
    /// </summary>
    public bool HasMultiplePanes => _panes.Count > 1;

    /// <summary>
    /// Gets the total number of panes in this tab.
    /// </summary>
    public int PaneCount => _panes.Count;

    /// <summary>
    /// Gets the node tree to be rendered by the UI: either the single zoomed pane or the complete split tree.
    /// </summary>
    public IPaneNodeViewModel DisplayRootNode =>
        IsZoomed && ActivePane != null
            ? new LeafPaneNodeViewModel(ActivePane)
            : RootPaneNode;

    /// <summary>
    /// Optional factory delegate used to instantiate new ConPTY sessions when splitting panes.
    /// If null, a session using the same shell type and working directory will be requested from the UI/service coordinator.
    /// </summary>
    public Func<string?, ShellType, IShellSession>? SessionFactory { get; set; }

    /// <summary>
    /// Initializes split pane tree tracking for the initial primary pane.
    /// </summary>
    private void InitializeSplitPanes(TerminalPaneViewModel initialPane)
    {
        _panes.Add(initialPane);
        ActivePane = initialPane;
        ActivePane.IsActive = true;
        RootPaneNode = new LeafPaneNodeViewModel(initialPane);
    }

    /// <summary>
    /// Sets the focused active pane within this tab and updates visual border highlights (REQ-UI-006).
    /// </summary>
    public void SetActivePane(TerminalPaneViewModel pane)
    {
        if (pane == null || ActivePane == pane) return;

        foreach (var p in _panes)
        {
            p.IsActive = p == pane;
        }

        ActivePane = pane;

        // Synchronize tab working directory and title from newly focused pane
        if (!string.IsNullOrWhiteSpace(pane.WorkingDirectory))
        {
            WorkingDirectory = pane.WorkingDirectory;
        }

        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(TabTooltip));
    }

    /// <summary>
    /// Splits the specified target pane in the given direction (Vertical = right, Horizontal = down) (REQ-UI-006).
    /// Inherits working directory and shell type from the target pane.
    /// </summary>
    public TerminalPaneViewModel SplitPane(
        TerminalPaneViewModel targetPane,
        PaneSplitDirection direction,
        IShellSession? customSession = null)
    {
        if (targetPane == null) throw new ArgumentNullException(nameof(targetPane));

        var leaf = RootPaneNode.FindLeaf(targetPane);
        if (leaf == null) throw new InvalidOperationException("Target pane was not found in layout tree.");

        IShellSession newSession = customSession ?? CreateSessionForSplit(targetPane);
        var newPane = new TerminalPaneViewModel(
            this,
            newSession,
            IsDarkTerminalTheme,
            TerminalFontSize,
            TerminalFontFamily);

        _panes.Add(newPane);

        var parent = RootPaneNode.FindParent(leaf);
        var newLeaf = new LeafPaneNodeViewModel(newPane);
        var splitNode = new SplitPaneNodeViewModel(direction, leaf, newLeaf, 0.5);

        if (parent is SplitPaneNodeViewModel splitParent)
        {
            if (splitParent.FirstChild == leaf)
            {
                splitParent.FirstChild = splitNode;
            }
            else
            {
                splitParent.SecondChild = splitNode;
            }
        }
        else
        {
            // leaf was the root node
            RootPaneNode = splitNode;
        }

        SetActivePane(newPane);
        newPane.StartSession();

        OnPropertyChanged(nameof(HasMultiplePanes));
        OnPropertyChanged(nameof(PaneCount));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(DisplayRootNode));
        LayoutChanged?.Invoke();

        return newPane;
    }

    /// <summary>
    /// Splits the currently active pane in the specified direction.
    /// </summary>
    public TerminalPaneViewModel SplitActivePane(PaneSplitDirection direction) =>
        SplitPane(ActivePane, direction);

    [RelayCommand]
    public void SplitRight() => SplitActivePane(PaneSplitDirection.Vertical);

    [RelayCommand]
    public void SplitDown() => SplitActivePane(PaneSplitDirection.Horizontal);

    /// <summary>
    /// Closes a specific terminal pane and gracefully collapses remaining panes (REQ-UI-006).
    /// If the last pane closes, the entire tab closes.
    /// </summary>
    public void ClosePane(TerminalPaneViewModel paneToClose)
    {
        if (paneToClose == null || !_panes.Contains(paneToClose)) return;

        var leaf = RootPaneNode.FindLeaf(paneToClose);
        if (leaf == null) return;

        paneToClose.Dispose();
        _panes.Remove(paneToClose);

        if (_panes.Count == 0)
        {
            RequestClose();
            return;
        }

        var parent = RootPaneNode.FindParent(leaf);
        if (parent is SplitPaneNodeViewModel splitParent)
        {
            var survivingSibling = splitParent.FirstChild == leaf
                ? splitParent.SecondChild
                : splitParent.FirstChild;

            var grandParent = RootPaneNode.FindParent(splitParent);
            if (grandParent is SplitPaneNodeViewModel grandSplit)
            {
                if (grandSplit.FirstChild == splitParent)
                {
                    grandSplit.FirstChild = survivingSibling;
                }
                else
                {
                    grandSplit.SecondChild = survivingSibling;
                }
            }
            else
            {
                RootPaneNode = survivingSibling;
            }
        }

        if (ActivePane == paneToClose || !ActivePane.IsActive)
        {
            SetActivePane(_panes[0]);
        }

        if (IsZoomed && _panes.Count <= 1)
        {
            IsZoomed = false;
        }

        OnPropertyChanged(nameof(HasMultiplePanes));
        OnPropertyChanged(nameof(PaneCount));
        OnPropertyChanged(nameof(DisplayTitle));
        OnPropertyChanged(nameof(DisplayRootNode));
        LayoutChanged?.Invoke();
    }

    [RelayCommand]
    public void CloseActivePane() => ClosePane(ActivePane);

    [RelayCommand]
    public void ToggleZoom()
    {
        if (_panes.Count <= 1)
        {
            IsZoomed = false;
            return;
        }

        IsZoomed = !IsZoomed;
        OnPropertyChanged(nameof(DisplayRootNode));
        LayoutChanged?.Invoke();
    }

    [RelayCommand]
    public void FocusNextPane()
    {
        if (_panes.Count <= 1) return;
        var currentIndex = _panes.IndexOf(ActivePane);
        var nextIndex = (currentIndex + 1) % _panes.Count;
        SetActivePane(_panes[nextIndex]);
    }

    [RelayCommand]
    public void FocusPreviousPane()
    {
        if (_panes.Count <= 1) return;
        var currentIndex = _panes.IndexOf(ActivePane);
        var prevIndex = (currentIndex - 1 + _panes.Count) % _panes.Count;
        SetActivePane(_panes[prevIndex]);
    }

    /// <summary>
    /// Navigates focus to the adjacent pane in the specified direction (Alt + ArrowKeys) (REQ-UI-006).
    /// </summary>
    public void NavigatePane(PaneNavigationDirection direction)
    {
        if (_panes.Count <= 1 || ActivePane == null) return;

        // In a flat list of panes, Left/Up moves backward, Right/Down moves forward
        if (direction is PaneNavigationDirection.Left or PaneNavigationDirection.Up)
        {
            FocusPreviousPane();
        }
        else
        {
            FocusNextPane();
        }
    }

    /// <summary>
    /// Resizes the split boundary adjacent to the active pane in the specified direction (Ctrl + Alt + ArrowKeys).
    /// </summary>
    public void ResizeActivePane(PaneNavigationDirection direction, double delta = 0.05)
    {
        if (_panes.Count <= 1 || ActivePane == null) return;

        var leaf = RootPaneNode.FindLeaf(ActivePane);
        if (leaf == null) return;

        var parent = RootPaneNode.FindParent(leaf);
        if (parent is SplitPaneNodeViewModel split)
        {
            if (split.Orientation == PaneSplitDirection.Vertical)
            {
                if (direction == PaneNavigationDirection.Left)
                {
                    split.SplitRatio = Math.Clamp(split.SplitRatio - delta, 0.1, 0.9);
                }
                else if (direction == PaneNavigationDirection.Right)
                {
                    split.SplitRatio = Math.Clamp(split.SplitRatio + delta, 0.1, 0.9);
                }
            }
            else // Horizontal
            {
                if (direction == PaneNavigationDirection.Up)
                {
                    split.SplitRatio = Math.Clamp(split.SplitRatio - delta, 0.1, 0.9);
                }
                else if (direction == PaneNavigationDirection.Down)
                {
                    split.SplitRatio = Math.Clamp(split.SplitRatio + delta, 0.1, 0.9);
                }
            }
        }
    }

    private IShellSession CreateSessionForSplit(TerminalPaneViewModel sourcePane)
    {
        var targetDir = !string.IsNullOrWhiteSpace(sourcePane.WorkingDirectory)
            ? sourcePane.WorkingDirectory
            : WorkingDirectory;

        if (SessionFactory != null)
        {
            return SessionFactory(targetDir, sourcePane.ShellType);
        }

        // Fallback: create local session if factory is not set
        return new ShellSession(
            $"{sourcePane.Title} (Split)",
            targetDir,
            sourcePane.ShellType);
    }
}
