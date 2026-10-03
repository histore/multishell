using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views.Dialogs;

public partial class TabSwitcherOverlayView : UserControl
{
    public event Action? TabSelected;
    public event Action? DismissRequested;

    public TabSwitcherOverlayView()
    {
        InitializeComponent();

        if (TabSwitcherOverlayGrid != null)
        {
            TabSwitcherOverlayGrid.PointerPressed += (_, e) =>
            {
                if (e.Source == TabSwitcherOverlayGrid && DataContext is MainViewModel vm)
                {
                    vm.CancelTabSwitcher();
                    DismissRequested?.Invoke();
                }
            };
        }

        if (TabSwitcherListBox != null)
        {
            TabSwitcherListBox.AddHandler(InputElement.PointerPressedEvent, OnTabSwitcherListBoxPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }
    }

    private void OnTabSwitcherListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            var clickedTab = GetTabItemFromPointerSource(e.Source as Visual);
            if (clickedTab != null && DataContext is MainViewModel vm)
            {
                if (IsTabSwitcherCloseButton(e.Source as Visual))
                {
                    e.Handled = true;
                    vm.CloseTabFromSwitcher(clickedTab);
                    return;
                }

                e.Handled = true;
                vm.SelectTabFromSwitcher(clickedTab);
                TabSelected?.Invoke();
            }
        }
    }

    private static TerminalTabViewModel? GetTabItemFromPointerSource(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is ListBoxItem item && item.DataContext is TerminalTabViewModel tab)
            {
                return tab;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static bool IsTabSwitcherCloseButton(Visual? visual)
    {
        while (visual != null && visual is not ListBoxItem)
        {
            if (visual is Button btn && btn.Classes.Contains("tabSwitcherCloseBtn"))
            {
                return true;
            }
            visual = visual.GetVisualParent();
        }
        return false;
    }
}
