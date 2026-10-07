using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class TerminalTabView
{
    private void OnSearchInputBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TerminalTabViewModel vm) return;

        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        if (e.Key == Key.Escape)
        {
            vm.CloseSearch();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter)
        {
            if (isShift)
            {
                vm.SearchPrevious();
            }
            else
            {
                vm.SearchNext();
            }
            e.Handled = true;
            return;
        }

        var isCtrlShift = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == (KeyModifiers.Control | KeyModifiers.Shift);
        if (e.Key == Key.F3 && isCtrlShift)
        {
            vm.SearchNext();
            e.Handled = true;
            return;
        }
    }

    private bool IsInsideSearchOverlay(object? source)
    {
        if (_searchOverlay == null || source is not Visual visual) return false;
        return ReferenceEquals(_searchOverlay, visual) || _searchOverlay.IsVisualAncestorOf(visual);
    }

    private void OnFocusSearchBoxRequested()
    {
        void DoFocusSearch()
        {
            _searchInputBox ??= this.FindControl<TextBox>("SearchInputBox");
            if (_searchInputBox != null)
            {
                _searchInputBox.Focus();
                _searchInputBox.SelectAll();
            }
        }

        Dispatcher.UIThread.Post(DoFocusSearch, DispatcherPriority.Input);
        Dispatcher.UIThread.Post(DoFocusSearch, DispatcherPriority.Loaded);
    }

    private void OnFocusTerminalRequested()
    {
        Dispatcher.UIThread.Post(() =>
        {
            Terminal.Focus();
        });
    }
}
