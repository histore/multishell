using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow
{
    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (TryHandleTabSwitcherKeyDown(e)) return;
        if (TryHandleCtrlShiftActionKeyDown(e)) return;
        if (HistoryDrawer?.IsDrawerOpen == true && HistoryDrawer.HandleKeyDown(e)) return;
        if (TryHandleActiveTabRenameKeyDown(e)) return;
        if (TryHandleFunctionKeyDown(e)) return;
        if (e.Key == Key.Escape && TryHandleEscapeKeyDown())
        {
            e.Handled = true;
            return;
        }

        TryHandleTabManagementKeyDown(e);
    }

    private bool TryHandleTabSwitcherKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MainViewModel switcherVm || !switcherVm.IsTabSwitcherOpen) return false;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        switch (e.Key)
        {
            case Key.Tab or Key.PageDown or Key.Down:
                switcherVm.AdvanceTabSwitcher(forward: !isShift);
                break;
            case Key.PageUp or Key.Up:
                switcherVm.AdvanceTabSwitcher(forward: false);
                break;
            case Key.Enter:
                switcherVm.CommitTabSwitcher();
                FocusActiveTerminal();
                break;
            case Key.Escape:
                switcherVm.CancelTabSwitcher();
                FocusActiveTerminal();
                break;
            default:
                return false;
        }

        e.Handled = true;
        return true;
    }

    private bool TryHandleCtrlShiftActionKeyDown(KeyEventArgs e)
    {
        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var isCtrlShift = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == (KeyModifiers.Control | KeyModifiers.Shift);
        if (isAlt || !isCtrlShift) return false;

        switch (e.Key)
        {
            case Key.H: OpenOrToggleHistoryDrawer(0); e.Handled = true; return true;
            case Key.L: OpenOrToggleHistoryDrawer(1); e.Handled = true; return true;
            case Key.P: OpenOrToggleHistoryDrawer(2); e.Handled = true; return true;
            case Key.O: _ = PickAndInsertFileAsync(); e.Handled = true; return true;
            case Key.B: _ = PickAndInsertFolderAsync(); e.Handled = true; return true;
            case Key.E: OpenActiveTabInFileBrowser(); e.Handled = true; return true;
            case Key.F:
                if (DataContext is MainViewModel searchVm && searchVm.SelectedTab != null)
                {
                    searchVm.SelectedTab.OpenSearch();
                    e.Handled = true;
                    return true;
                }
                return false;
            case Key.C: return TryHandleCtrlShiftCopyKeyDown(e);
            case Key.V: return TryHandleCtrlShiftPasteKeyDown(e);
            default: return false;
        }
    }

    private bool TryHandleCtrlShiftCopyKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MainViewModel cVm || cVm.SelectedTab?.ActivePane == null) return false;
        var pane = cVm.SelectedTab.ActivePane;
        var term = GetActiveTerminalControl();
        var hasSelection = (term?.HasSelection == true) || pane.TerminalModel.HasSelection;
        if (!hasSelection) return false;

        var rawText = (term != null && !string.IsNullOrEmpty(term.SelectedText)) ? term.SelectedText : pane.TerminalModel.SelectedText;
        var text = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
        if (!string.IsNullOrEmpty(text) && Clipboard != null)
        {
            _ = Clipboard.SetTextAsync(text);
            pane.TerminalModel.ClearSelection();
        }
        e.Handled = true;
        return true;
    }

    private bool TryHandleCtrlShiftPasteKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MainViewModel pVm || pVm.SelectedTab?.ActivePane == null) return false;
        var pane = pVm.SelectedTab.ActivePane;
        if (Clipboard == null || !pane.IsRunning) return false;

        _ = PasteToPaneAsync(pane);
        e.Handled = true;
        return true;
    }

    private bool TryHandleActiveTabRenameKeyDown(KeyEventArgs e)
    {
        if (DataContext is not MainViewModel rVm || rVm.SelectedTab?.IsRenaming != true) return false;
        if (e.Key == Key.Enter)
        {
            rVm.SelectedTab.CommitRenaming();
            FocusActiveTerminal();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.Escape)
        {
            rVm.SelectedTab.CancelRenaming();
            FocusActiveTerminal();
            e.Handled = true;
            return true;
        }
        return false;
    }

    private bool TryHandleFunctionKeyDown(KeyEventArgs e)
    {
        if (!FunctionKeyRouting.IsFunctionKey(e.Key)) return false;
        if (!FunctionKeyRouting.ShouldInterceptFunctionKey(e.Key, e.KeyModifiers)) return true;

        if (e.Key == Key.F1)
        {
            ShowHelpModal();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.F2 && DataContext is MainViewModel f2Vm && f2Vm.SelectedTab != null)
        {
            f2Vm.SelectedTab.StartRenaming();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.F3 && DataContext is MainViewModel f3Vm && f3Vm.SelectedTab?.IsSearchOpen == true)
        {
            f3Vm.SelectedTab.SearchNext();
            e.Handled = true;
            return true;
        }
        if (e.Key == Key.F4 && DataContext is MainViewModel f4Vm)
        {
            f4Vm.CloseSelectedTab();
            e.Handled = true;
            return true;
        }

        return true;
    }

    private bool TryHandleEscapeKeyDown()
    {
        if (DataContext is MainViewModel dvm && dvm.IsTabSwitcherOpen)
        {
            dvm.CancelTabSwitcher();
            FocusActiveTerminal();
            return true;
        }
        if (HelpModal?.IsVisible == true) { HideHelpModal(); return true; }
        if (AboutModal?.IsVisible == true) { HideAboutModal(); return true; }
        if (ProfilesModal?.IsVisible == true && DataContext is MainViewModel pvm) { pvm.CloseProfilesModal(); return true; }
        if (HistoryDrawer?.IsDrawerOpen == true) { HideHistoryDrawerAndFocusTerminal(); return true; }
        if (DataContext is MainViewModel tvm && tvm.SelectedTab?.IsSearchOpen == true)
        {
            tvm.SelectedTab.CloseSearch();
            FocusActiveTerminal();
            return true;
        }
        return false;
    }

    private void TryHandleTabManagementKeyDown(KeyEventArgs e)
    {
        if (HelpModal?.IsVisible == true || AboutModal?.IsVisible == true || ProfilesModal?.IsVisible == true) return;
        if (DataContext is not MainViewModel vm) return;

        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0 && !isAlt;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        if (!isCtrl) return;

        if (isShift)
        {
            if (e.Key == Key.PageUp) { vm.MoveSelectedTab(-1); e.Handled = true; return; }
            if (e.Key == Key.PageDown) { vm.MoveSelectedTab(1); e.Handled = true; return; }
            if (e.Key is Key.W or Key.F4) { vm.CloseSelectedTab(); e.Handled = true; return; }
            if (e.Key == Key.Tab) { vm.ShowTabSwitcher(forward: false, isKeyboardTriggered: true); e.Handled = true; }
            return;
        }

        if (TryHandleCtrlTabShortcut(vm, e.Key))
        {
            e.Handled = true;
        }
    }

    private static bool TryHandleCtrlTabShortcut(MainViewModel vm, Key key)
    {
        switch (key)
        {
            case Key.Tab: vm.ShowTabSwitcher(forward: true, isKeyboardTriggered: true); return true;
            case Key.PageUp: vm.CyclePreviousTab(); return true;
            case Key.PageDown: vm.CycleNextTab(); return true;
            case >= Key.D1 and <= Key.D8: vm.SelectTabByIndex(key - Key.D1); return true;
            case >= Key.NumPad1 and <= Key.NumPad8: vm.SelectTabByIndex(key - Key.NumPad1); return true;
            case Key.D9 or Key.NumPad9: vm.SelectTabByIndex(-1); return true;
            case Key.OemPlus or Key.Add: vm.IncreaseTerminalFontSize(); return true;
            case Key.OemMinus or Key.Subtract: vm.DecreaseTerminalFontSize(); return true;
            case Key.D0 or Key.NumPad0: vm.ResetTerminalFontSize(); return true;
            default: return false;
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

    private static bool IsSourceInsideTextBox(object? source)
    {
        return source is Visual visual && (visual is TextBox || visual.FindAncestorOfType<TextBox>() != null);
    }

    private async Task PickAndInsertFileAsync()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = vm.Loc["Dialog_PickFile_Title"],
            AllowMultiple = false
        };

        var initialDir = !string.IsNullOrWhiteSpace(vm.SelectedTab.WorkingDirectory)
            ? vm.SelectedTab.WorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrWhiteSpace(initialDir) && System.IO.Directory.Exists(initialDir))
        {
            try
            {
                var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDir);
                if (folder != null)
                {
                    options.SuggestedStartLocation = folder;
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow.Keyboard] StorageProvider suggested folder lookup failed: {ex.Message}");
            }
        }

        try
        {
            var results = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            if (results.Count > 0)
            {
                var path = results[0].TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    vm.SelectedTab.InsertPath(path);
                }
            }
        }
        finally
        {
            FocusActiveTerminal();
        }
    }

    private async Task PickAndInsertFolderAsync()
    {
        if (DataContext is not MainViewModel vm || vm.SelectedTab == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = vm.Loc["Dialog_PickFolder_Title"],
            AllowMultiple = false
        };

        var initialDir = !string.IsNullOrWhiteSpace(vm.SelectedTab.WorkingDirectory)
            ? vm.SelectedTab.WorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrWhiteSpace(initialDir) && System.IO.Directory.Exists(initialDir))
        {
            try
            {
                var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDir);
                if (folder != null)
                {
                    options.SuggestedStartLocation = folder;
                }
            }
            catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow.Keyboard] StorageProvider suggested folder lookup failed: {ex.Message}");
            }
        }

        try
        {
            var results = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (results.Count > 0)
            {
                var path = results[0].TryGetLocalPath();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    vm.SelectedTab.InsertPath(path);
                }
            }
        }
        finally
        {
            FocusActiveTerminal();
        }
    }

    private void OpenActiveTabInFileBrowser()
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            vm.SelectedTab.OpenInFileBrowser();
        }
    }

    private async Task PasteToPaneAsync(TerminalPaneViewModel pane)
    {
        if (Clipboard == null || !pane.IsRunning) return;
        var text = await Clipboard.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            pane.PasteText(text);
        }
    }
}
