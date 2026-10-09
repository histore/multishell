using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class TerminalTabView
{
    private async void OnTerminalKeyDown(object? sender, KeyEventArgs e)
    {
        // 1. Prevent standalone modifier keys (Ctrl, Shift, Alt, Meta) from destroying active text selection
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var isAltGr = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == (KeyModifiers.Control | KeyModifiers.Alt);
        if (DataContext is not TerminalTabViewModel vm) return;

        vm.IsAltGrActive = isAltGr;

        if (isAltGr)
        {
            if (vm.IsRunning)
            {
                var text = ResolveAltGrText(e);
                if (!string.IsNullOrEmpty(text))
                {
                    vm.SendInput(Encoding.UTF8.GetBytes(text));
                    e.Handled = true;
                }
            }
            return;
        }

        var isAltGrOrAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0 && !isAltGrOrAlt;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

        // 2. Zoom & Font-Size Shortcuts (REQ-UI-005)
        if (isCtrl && !isShift)
        {
            if (e.Key is Key.OemPlus or Key.Add)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.DataContext is MainViewModel mainVm)
                {
                    mainVm.IncreaseTerminalFontSize();
                    e.Handled = true;
                    return;
                }
            }
            if (e.Key is Key.OemMinus or Key.Subtract)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.DataContext is MainViewModel mainVm)
                {
                    mainVm.DecreaseTerminalFontSize();
                    e.Handled = true;
                    return;
                }
            }
            if (e.Key is Key.D0 or Key.NumPad0)
            {
                var topLevel = TopLevel.GetTopLevel(this);
                if (topLevel?.DataContext is MainViewModel mainVm)
                {
                    mainVm.ResetTerminalFontSize();
                    e.Handled = true;
                    return;
                }
            }
        }

        // 3. Terminal Scrollback & Buffer Control Shortcuts (REQ-TERM-003)
        // 3a. Shift+PageUp / Shift+PageDown: Scroll viewport through scrollback buffer
        if (isShift && !isCtrl)
        {
            if (e.Key == Key.PageUp)
            {
                vm.PageUp();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.PageDown)
            {
                vm.PageDown();
                e.Handled = true;
                return;
            }
        }
        else if (!isShift && !isCtrl)
        {
            if (e.Key == Key.PageUp)
            {
                if (vm.IsRunning)
                {
                    vm.SendPageUp(isAltGrOrAlt);
                }
                e.Handled = true;
                return;
            }
            if (e.Key == Key.PageDown)
            {
                if (vm.IsRunning)
                {
                    vm.SendPageDown(isAltGrOrAlt);
                }
                e.Handled = true;
                return;
            }
        }

        // 3b. Ctrl+Shift+K: Clear terminal buffer and screen
        if (isCtrl && isShift && e.Key == Key.K)
        {
            vm.ClearBuffer();
            e.Handled = true;
            return;
        }

        // 3c. Ctrl+Shift+F: In-Terminal Text Search Overlay (REQ-TERM-006)
        if (isCtrl && isShift && e.Key == Key.F)
        {
            vm.OpenSearch();
            e.Handled = true;
            return;
        }

        // Prevent Ctrl+Shift+O, Ctrl+Shift+B, Ctrl+Shift+E, and Ctrl+Shift+P from leaking VT sequences to shell
        if (isCtrl && isShift && (e.Key is Key.O or Key.B or Key.E or Key.P))
        {
            e.Handled = true;
            return;
        }

        // When search overlay is open, Escape closes search and Ctrl+Shift+F3 navigates matches
        if (vm.IsSearchOpen)
        {
            if (e.Key == Key.Escape)
            {
                vm.CloseSearch();
                e.Handled = true;
                return;
            }

            if (isCtrl && isShift && e.Key == Key.F3)
            {
                vm.SearchNext();
                e.Handled = true;
                return;
            }
        }

        // 3d. Ctrl+Shift+C: Copy selected text without sending interrupt signals
        if (isCtrl && isShift && e.Key == Key.C)
        {
            var rawText = vm.TerminalModel.HasSelection ? vm.TerminalModel.SelectedText : string.Empty;
            var text = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
            if (!string.IsNullOrEmpty(text))
            {
                var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
            }
            e.Handled = true;
            return;
        }

        // 3d. Ctrl+Shift+V: Paste from clipboard without sending interrupt signals
        if (isCtrl && isShift && e.Key == Key.V)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                var text = await clipboard.TryGetTextAsync();
                if (!string.IsNullOrEmpty(text) && vm.IsRunning)
                {
                    vm.SendInput(Encoding.UTF8.GetBytes(text));
                }
            }
            e.Handled = true;
            return;
        }

        // 4. Ctrl+C with active selection -> Copy to clipboard and prevent sending \x03
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.C)
        {
            if (vm.TerminalModel.HasSelection)
            {
                var rawText = vm.TerminalModel.SelectedText;
                var text = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
                if (!string.IsNullOrEmpty(text))
                {
                    var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(text);
                    }
                }
                e.Handled = true;
                return;
            }
            // Without selection: let default handler send \x03 (SIGINT)
        }

        // 5. Ctrl+V -> Paste from clipboard into terminal
        if (e.KeyModifiers == KeyModifiers.Control && e.Key == Key.V)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard != null)
            {
                var text = await clipboard.TryGetTextAsync();
                if (!string.IsNullOrEmpty(text) && vm.IsRunning)
                {
                    vm.SendInput(Encoding.UTF8.GetBytes(text));
                }
            }
            e.Handled = true;
            return;
        }

        // 6. Ctrl+Enter or Shift+Enter -> Send Linefeed (\n / 0x0A) for multi-line script continuation without executing command
        if (e.Key == Key.Enter && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) != 0)
        {
            if (vm.IsRunning)
            {
                vm.SendInput([0x0A]);
            }
            e.Handled = true;
            return;
        }
    }

    private static string? ResolveAltGrText(KeyEventArgs e)
    {
        // 1. Check KeySymbol from Avalonia if available and printable
        if (!string.IsNullOrEmpty(e.KeySymbol) && !char.IsControl(e.KeySymbol[0]))
        {
            return e.KeySymbol;
        }

        // 2. Direct mapping for German and international AltGr combinations
        return e.Key switch
        {
            Key.Q => "@",
            Key.E => "€",
            Key.D7 => "{",
            Key.D8 => "[",
            Key.D9 => "]",
            Key.D0 => "}",
            Key.OemMinus or Key.OemBackslash or Key.Oem4 => "\\",
            Key.OemPlus or Key.Oem6 => "~",
            Key.Oem102 or Key.OemPipe or Key.Oem5 or Key.OemQuestion => "|",
            Key.M => "µ",
            Key.D2 => "²",
            Key.D3 => "³",
            _ => null
        };
    }

    private void OnTerminalKeyUp(object? sender, KeyEventArgs e)
    {
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || e.Key is Key.LeftCtrl or Key.RightCtrl)
        {
            HideLinkHighlight();
            Terminal.Cursor = Cursor.Default;
        }

        var isAltGr = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == (KeyModifiers.Control | KeyModifiers.Alt);
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt)
        {
            isAltGr = false;
        }

        if (DataContext is TerminalTabViewModel vm)
        {
            vm.IsAltGrActive = isAltGr;
        }

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }
    }
}
