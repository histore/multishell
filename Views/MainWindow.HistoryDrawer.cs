using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow
{
    public void ToggleHistoryDrawer()
    {
        HistoryDrawer?.ToggleHistoryDrawer();
    }

    public void OpenOrToggleHistoryDrawer(int targetTabIndex)
    {
        HistoryDrawer?.OpenOrToggleHistoryDrawer(targetTabIndex);
    }

    public void ShowHistoryDrawer()
    {
        HistoryDrawer?.ShowHistoryDrawer();
    }

    public void HideHistoryDrawerAndFocusTerminal()
    {
        HistoryDrawer?.HideHistoryDrawer();
        FocusActiveTerminal();
    }
}
