using System;
using Avalonia.Input;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow
{
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Tab Switcher Active Keyboard Handling (REQ-TAB-019)
        if (DataContext is MainViewModel switcherVm && switcherVm.IsTabSwitcherOpen)
        {
            var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

            if (e.Key == Key.Tab || e.Key == Key.PageDown || e.Key == Key.Down)
            {
                switcherVm.AdvanceTabSwitcher(forward: !isShift);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.PageUp || e.Key == Key.Up)
            {
                switcherVm.AdvanceTabSwitcher(forward: false);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                switcherVm.CommitTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                switcherVm.CancelTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
        }

        // Ctrl+Shift+H: Open Command History / Ctrl+Shift+L: Open Directory History (REQ-TAB-015)
        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var isCtrlShift = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == (KeyModifiers.Control | KeyModifiers.Shift);
        if (!isAlt && isCtrlShift)
        {
            if (e.Key == Key.H)
            {
                OpenOrToggleHistoryDrawer(0);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.L)
            {
                OpenOrToggleHistoryDrawer(1);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.F)
            {
                if (DataContext is MainViewModel searchVm && searchVm.SelectedTab != null)
                {
                    searchVm.SelectedTab.OpenSearch();
                    e.Handled = true;
                    return;
                }
            }
        }

        // When History Drawer is open, capture all navigation keys globally
        if (HistoryDrawer?.IsVisible == true)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    if (HistoryTabControl?.SelectedIndex == 1 && !string.IsNullOrEmpty(vm.SelectedTab.DirectoryFilterQuery))
                    {
                        vm.SelectedTab.DirectoryFilterQuery = string.Empty;
                        e.Handled = true;
                        return;
                    }
                    if (HistoryTabControl?.SelectedIndex == 0 && !string.IsNullOrEmpty(vm.SelectedTab.CommandFilterQuery))
                    {
                        vm.SelectedTab.CommandFilterQuery = string.Empty;
                        e.Handled = true;
                        return;
                    }
                }
                HideHistoryDrawerAndFocusTerminal();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (HistoryTabControl?.SelectedIndex == 1)
                {
                    PasteSelectedDirectory();
                }
                else
                {
                    PasteSelectedCommand();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                NavigateHistorySelection(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                NavigateHistorySelection(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = 0;
                    var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = 1;
                    var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery);
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = HistoryTabControl.SelectedIndex == 0 ? 1 : 0;
                    var hasFilter = false;
                    if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                    {
                        hasFilter = HistoryTabControl.SelectedIndex == 1
                            ? !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery)
                            : !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
                    }
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
                e.Handled = true;
                return;
            }
        }

        // F1: Help Modal
        if (e.Key == Key.F1)
        {
            ShowHelpModal();
            e.Handled = true;
            return;
        }

        // F3 / Shift+F3: In-Terminal Search Match Navigation (REQ-TERM-006)
        if (e.Key == Key.F3 && DataContext is MainViewModel f3Vm && f3Vm.SelectedTab?.IsSearchOpen == true)
        {
            var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            if (isShift)
            {
                f3Vm.SelectedTab.SearchPrevious();
            }
            else
            {
                f3Vm.SelectedTab.SearchNext();
            }
            e.Handled = true;
            return;
        }

        // Escape: Close active dialog or drawer
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainViewModel dvm && dvm.IsTabSwitcherOpen)
            {
                dvm.CancelTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
            if (HelpModal?.IsVisible == true)
            {
                HideHelpModal();
                e.Handled = true;
                return;
            }
            if (AboutModal?.IsVisible == true)
            {
                HideAboutModal();
                e.Handled = true;
                return;
            }
            if (ProfilesModal?.IsVisible == true && DataContext is MainViewModel pvm)
            {
                pvm.CloseProfilesModal();
                e.Handled = true;
                return;
            }
            if (HistoryDrawer?.IsVisible == true)
            {
                HideHistoryDrawerAndFocusTerminal();
                e.Handled = true;
                return;
            }
            if (DataContext is MainViewModel tvm && tvm.SelectedTab?.IsSearchOpen == true)
            {
                tvm.SelectedTab.CloseSearch();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
        }

        // Only process Tab management shortcuts if modal dialogs are not capturing input
        if (HelpModal?.IsVisible != true && AboutModal?.IsVisible != true && ProfilesModal?.IsVisible != true)
        {
            if (DataContext is MainViewModel vm)
            {
                var isAltGrOrAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
                var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0 && !isAltGrOrAlt;
                var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

                // 1. Ctrl+Shift+PageUp / Ctrl+Shift+PageDown: Move Tab Left/Right
                if (isCtrl && isShift && e.Key == Key.PageUp)
                {
                    vm.MoveSelectedTab(-1);
                    e.Handled = true;
                    return;
                }
                if (isCtrl && isShift && e.Key == Key.PageDown)
                {
                    vm.MoveSelectedTab(1);
                    e.Handled = true;
                    return;
                }

                // 2. Ctrl+Shift+W: Close active tab
                if (isCtrl && isShift && e.Key == Key.W)
                {
                    vm.CloseSelectedTab();
                    e.Handled = true;
                    return;
                }

                // 3. Ctrl+Tab / Ctrl+Shift+Tab: Unified Tab Switcher HUD (REQ-TAB-019)
                if (isCtrl && e.Key == Key.Tab)
                {
                    vm.ShowTabSwitcher(forward: !isShift, isKeyboardTriggered: true);
                    e.Handled = true;
                    return;
                }

                // 4. Ctrl+PageUp / Ctrl+PageDown: Previous / Next Tab
                if (isCtrl && !isShift && e.Key == Key.PageUp)
                {
                    vm.CyclePreviousTab();
                    e.Handled = true;
                    return;
                }
                if (isCtrl && !isShift && e.Key == Key.PageDown)
                {
                    vm.CycleNextTab();
                    e.Handled = true;
                    return;
                }

                // 5. Ctrl+1 .. Ctrl+8: Jump to specific tab index (0..7)
                if (isCtrl && !isShift)
                {
                    if (e.Key >= Key.D1 && e.Key <= Key.D8)
                    {
                        vm.SelectTabByIndex(e.Key - Key.D1);
                        e.Handled = true;
                        return;
                    }
                    if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad8)
                    {
                        vm.SelectTabByIndex(e.Key - Key.NumPad1);
                        e.Handled = true;
                        return;
                    }
                    // 6. Ctrl+9: Jump to last tab
                    if (e.Key is Key.D9 or Key.NumPad9)
                    {
                        vm.SelectTabByIndex(-1);
                        e.Handled = true;
                        return;
                    }

                    // 7. Zoom & Font-Size Shortcuts (REQ-UI-005)
                    if (e.Key is Key.OemPlus or Key.Add)
                    {
                        vm.IncreaseTerminalFontSize();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key is Key.OemMinus or Key.Subtract)
                    {
                        vm.DecreaseTerminalFontSize();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key is Key.D0 or Key.NumPad0)
                    {
                        vm.ResetTerminalFontSize();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsTabSwitcherOpen && vm.TabSwitcherIsKeyboardTriggered)
        {
            // When Ctrl key is released (or Ctrl is no longer present in modifier flags), commit tab switch
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || (e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                vm.CommitTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
            }
        }
    }
}
