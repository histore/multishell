using System.Text;
using Avalonia.Input;
using MultiShell.Models;
using MultiShell.ViewModels;

namespace MultiShell.Views;

/// <summary>
/// Partial class handling terminal keyboard input, AltGr workaround, clipboard shortcuts,
/// scrollback PageUp/PageDown routing, and split-pane navigation shortcuts.
/// </summary>
public partial class TerminalPaneView
{
    private void ResetAltGrState()
    {
        if (DataContext is TerminalPaneViewModel pane)
        {
            pane.Tab.IsAltGrActive = false;
        }
    }

    private void OnTerminalKeyUp(object? sender, KeyEventArgs e)
    {
        // Standalone modifier keys: mark handled so terminal control doesn't misprocess
        if (IsModifierKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        // AltGr is reported as Ctrl+Alt; clear once either modifier is released.
        var modifiers = e.KeyModifiers;
        var stillAltGr = (modifiers & KeyModifiers.Control) != 0 && (modifiers & KeyModifiers.Alt) != 0;
        if (!stillAltGr)
        {
            ResetAltGrState();
        }
    }

    private void OnTerminalKeyDown(object? sender, KeyEventArgs e)
    {
        // Prevent standalone modifier keys (Ctrl, Shift, Alt, Meta) from destroying active text selection
        if (IsModifierKey(e.Key))
        {
            e.Handled = true;
            return;
        }

        if (DataContext is not TerminalPaneViewModel vm) return;

        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        if (TryHandleAltGrAndLineFeed(vm, e, isCtrl, isAlt, isShift)) return;
        if (TryHandlePageNavigation(vm, e, isCtrl, isAlt)) return;
        if (TryHandleClipboardShortcuts(vm, e, isCtrl, isAlt, isShift)) return;
        if (TryHandlePaneShortcuts(vm, e, isCtrl, isAlt, isShift)) return;

        // In-Terminal Search (Ctrl + Shift + F)
        if (isCtrl && isShift && !isAlt && e.Key == Key.F)
        {
            vm.OpenSearch();
            e.Handled = true;
        }
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift
            or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin;

    private static bool TryHandleAltGrAndLineFeed(TerminalPaneViewModel vm, KeyEventArgs e, bool isCtrl, bool isAlt, bool isShift)
    {
        // AltGr (e.g. AltGr+Q = '@' on German layouts) arrives as Ctrl+Alt. Flag it so the
        // terminal control's bogus Ctrl+<letter> control byte is dropped and only the text input passes.
        vm.Tab.IsAltGrActive = isCtrl && isAlt;

        if (isCtrl && isAlt && !string.IsNullOrEmpty(e.KeySymbol) && !char.IsControl(e.KeySymbol[0]))
        {
            e.Handled = true;
            if (vm.IsRunning)
            {
                vm.SendInput(Encoding.UTF8.GetBytes(e.KeySymbol));
            }
            return true;
        }

        // Shift+Enter / Ctrl+Enter: Insert LF (0x0A) for multiline prompts without submitting.
        if (e.Key is Key.Enter or Key.Return && (isShift || isCtrl) && !isAlt)
        {
            e.Handled = true;
            if (vm.IsRunning)
            {
                vm.SendInput([0x0A]);
            }
            return true;
        }

        return false;
    }

    private static bool TryHandlePageNavigation(TerminalPaneViewModel vm, KeyEventArgs e, bool isCtrl, bool isAlt)
    {
        if (e.Key is not (Key.PageUp or Key.PageDown) || isCtrl) return false;

        var isAltBuffer = vm.TerminalModel.IsAlternateBufferActive;
        if (isAltBuffer || isAlt)
        {
            if (e.Key == Key.PageUp) vm.SendPageUp(alt: isAlt);
            else vm.SendPageDown(alt: isAlt);
        }
        else
        {
            if (e.Key == Key.PageUp) vm.PageUp();
            else vm.PageDown();
        }

        e.Handled = true;
        return true;
    }

    private bool TryHandleClipboardShortcuts(TerminalPaneViewModel vm, KeyEventArgs e, bool isCtrl, bool isAlt, bool isShift)
    {
        if (isCtrl && !isAlt)
        {
            if (e.Key == Key.C && isShift)
            {
                e.Handled = true;
                _ = TryCopySelectionAsync(vm);
                return true;
            }

            if (e.Key == Key.C && !isShift)
            {
                e.Handled = true;
                var hasSelection = vm.TerminalModel.HasSelection || Terminal.HasSelection;
                if (hasSelection) _ = TryCopySelectionAsync(vm);
                else vm.SendInput([0x03]);
                return true;
            }

            if (e.Key == Key.V)
            {
                e.Handled = true;
                _ = PasteFromClipboardAsync(vm);
                return true;
            }
        }

        if (isShift && !isCtrl && !isAlt && e.Key == Key.Insert)
        {
            e.Handled = true;
            _ = PasteFromClipboardAsync(vm);
            return true;
        }

        return false;
    }

    private static bool TryHandlePaneShortcuts(TerminalPaneViewModel vm, KeyEventArgs e, bool isCtrl, bool isAlt, bool isShift)
    {
        if (isAlt && isShift && !isCtrl)
        {
            if (e.Key is Key.Right or Key.V) { vm.Tab.SplitRight(); e.Handled = true; return true; }
            if (e.Key is Key.Down or Key.H) { vm.Tab.SplitDown(); e.Handled = true; return true; }
            if (e.Key == Key.Z) { vm.Tab.ToggleZoom(); e.Handled = true; return true; }
        }

        if (isAlt && !isShift && !isCtrl && TryMapArrowDirection(e.Key, out var navDir))
        {
            vm.Tab.NavigatePane(navDir);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.F6)
        {
            if (isShift) vm.Tab.FocusPreviousPane();
            else vm.Tab.FocusNextPane();
            e.Handled = true;
            return true;
        }

        if (isCtrl && isAlt && !isShift && TryMapArrowDirection(e.Key, out var resizeDir))
        {
            vm.Tab.ResizeActivePane(resizeDir);
            e.Handled = true;
            return true;
        }

        if (isCtrl && isShift && !isAlt && e.Key == Key.W && vm.Tab.HasMultiplePanes)
        {
            vm.Tab.CloseActivePane();
            e.Handled = true;
            return true;
        }

        return false;
    }

    private static bool TryMapArrowDirection(Key key, out PaneNavigationDirection direction)
    {
        switch (key)
        {
            case Key.Left: direction = PaneNavigationDirection.Left; return true;
            case Key.Right: direction = PaneNavigationDirection.Right; return true;
            case Key.Up: direction = PaneNavigationDirection.Up; return true;
            case Key.Down: direction = PaneNavigationDirection.Down; return true;
            default: direction = default; return false;
        }
    }
}
