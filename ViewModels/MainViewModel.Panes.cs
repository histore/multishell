using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class exposing split-pane orchestration commands delegated to the active terminal tab.
/// </summary>
public partial class MainViewModel
{
    /// <summary>
    /// Closes the active pane if the tab contains multiple panes, otherwise closes the entire tab (REQ-UI-006).
    /// </summary>
    [RelayCommand]
    public void CloseActivePaneOrTab()
    {
        if (SelectedTab == null) return;
        if (SelectedTab.HasMultiplePanes)
        {
            SelectedTab.CloseActivePane();
        }
        else
        {
            CloseTab(SelectedTab);
        }
    }

    [RelayCommand]
    public void SplitActiveTabRight() => SelectedTab?.SplitRight();

    [RelayCommand]
    public void SplitActiveTabDown() => SelectedTab?.SplitDown();

    [RelayCommand]
    public void ToggleZoomActiveTab() => SelectedTab?.ToggleZoom();

    [RelayCommand]
    public void FocusNextPaneActiveTab() => SelectedTab?.FocusNextPane();

    [RelayCommand]
    public void FocusPreviousPaneActiveTab() => SelectedTab?.FocusPreviousPane();

    [RelayCommand]
    public void NavigateActiveTabLeft() => SelectedTab?.NavigatePane(PaneNavigationDirection.Left);

    [RelayCommand]
    public void NavigateActiveTabRight() => SelectedTab?.NavigatePane(PaneNavigationDirection.Right);

    [RelayCommand]
    public void NavigateActiveTabUp() => SelectedTab?.NavigatePane(PaneNavigationDirection.Up);

    [RelayCommand]
    public void NavigateActiveTabDown() => SelectedTab?.NavigatePane(PaneNavigationDirection.Down);

    [RelayCommand]
    public void ResizeActiveTabLeft() => SelectedTab?.ResizeActivePane(PaneNavigationDirection.Left);

    [RelayCommand]
    public void ResizeActiveTabRight() => SelectedTab?.ResizeActivePane(PaneNavigationDirection.Right);

    [RelayCommand]
    public void ResizeActiveTabUp() => SelectedTab?.ResizeActivePane(PaneNavigationDirection.Up);

    [RelayCommand]
    public void ResizeActiveTabDown() => SelectedTab?.ResizeActivePane(PaneNavigationDirection.Down);
}
