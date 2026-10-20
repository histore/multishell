using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;
using MultiShell.Services;
using MultiShell.ViewModels;
using System.Threading.Tasks;
using SvcSystems.UI.Terminal;

namespace MultiShell.Views;

public partial class TerminalPaneView
{
    private void OnTerminalPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        if (!isCtrl) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.DataContext is MainViewModel mainVm)
        {
            if (e.Delta.Y > 0)
            {
                mainVm.IncreaseTerminalFontSize();
            }
            else if (e.Delta.Y < 0)
            {
                mainVm.DecreaseTerminalFontSize();
            }
            e.Handled = true;
        }
    }

    private IClipboard? GetClipboard()
    {
        return TopLevel.GetTopLevel(Terminal)?.Clipboard
            ?? TopLevel.GetTopLevel(this)?.Clipboard
            ?? (Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow?.Clipboard;
    }

    private void OnTerminalPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not TerminalPaneViewModel vm) return;

        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
        if (!isCtrl)
        {
            HideLinkHighlight();
            Terminal.Cursor = Cursor.Default;
            return;
        }

        var point = e.GetCurrentPoint(Terminal);
        if (TryGetTerminalWordOrLine(Terminal, vm, point.Position, out var lineText, out var colIndex, out var row, out var charWidth, out var charHeight))
        {
            var link = LinkDetectionHelper.ExtractLinkAtColumn(lineText, colIndex, vm.WorkingDirectory);
            if (link != null)
            {
                ShowLinkHighlight(link.StartIndex * charWidth, row * charHeight, link.Length * charWidth, charHeight);
                Terminal.Cursor = new Cursor(StandardCursorType.Hand);
                return;
            }
        }

        HideLinkHighlight();
        Terminal.Cursor = Cursor.Default;
    }

    private void ShowLinkHighlight(double x, double y, double width, double height)
    {
        _hoverLinkBorder ??= this.FindControl<Border>("HoverLinkBorder");
        if (_hoverLinkBorder == null) return;
        Canvas.SetLeft(_hoverLinkBorder, x);
        Canvas.SetTop(_hoverLinkBorder, y);
        _hoverLinkBorder.Width = Math.Max(0, width);
        _hoverLinkBorder.Height = Math.Max(0, height);
        _hoverLinkBorder.IsVisible = true;
    }

    private void HideLinkHighlight()
    {
        _hoverLinkBorder ??= this.FindControl<Border>("HoverLinkBorder");
        if (_hoverLinkBorder != null)
        {
            _hoverLinkBorder.IsVisible = false;
        }
    }

    private async void OnTerminalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(Terminal);
        if (DataContext is not TerminalPaneViewModel vm) return;

        // When interacting with the terminal via pointer, suppress VT mouse tracking mode so that
        // TerminalControl performs its native text selection, word selection, and highlighting smoothly
        // without sending raw mouse tracking escape sequences to interactive CLIs like Copilot CLI.
        vm.SuppressMouseTrackingMode();

        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        // 1. Ctrl + Left-Click: Open Clickable Hyperlink or Local File Path (REQ-TERM-005)
        if (point.Properties.IsLeftButtonPressed && isCtrl)
        {
            if (TryGetTerminalWordOrLine(Terminal, vm, point.Position, out var lineText, out var colIndex, out _, out _, out _))
            {
                var link = LinkDetectionHelper.ExtractLinkAtColumn(lineText, colIndex, vm.WorkingDirectory);
                if (link != null)
                {
                    if (LinkDetectionHelper.OpenTarget(link.ResolvedTarget, vm.WorkingDirectory))
                    {
                        e.Handled = true;
                        return;
                    }
                }
                else if (vm.TerminalModel.HasSelection || Terminal.HasSelection)
                {
                    var selected = (!string.IsNullOrEmpty(Terminal.SelectedText) ? Terminal.SelectedText : vm.TerminalModel.SelectedText).Trim();
                    if (LinkDetectionHelper.OpenTarget(selected, vm.WorkingDirectory))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        // 2. Right-Click: Instant Copy if text is selected, or Paste if no selection (REQ-TERM-004 / REQ-TAB-017)
        if (point.Properties.IsRightButtonPressed)
        {
            e.Handled = true;
            var hasSelection = vm.TerminalModel.HasSelection || Terminal.HasSelection;
            if (hasSelection)
            {
                var rawText = !string.IsNullOrEmpty(Terminal.SelectedText) ? Terminal.SelectedText : vm.TerminalModel.SelectedText;
                var text = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
                if (!string.IsNullOrEmpty(text))
                {
                    var clipboard = GetClipboard();
                    if (clipboard != null)
                    {
                        await clipboard.SetTextAsync(text);
                    }
                    vm.TerminalModel.ClearSelection();
                }
                return;
            }

            // If no text is selected: paste from clipboard directly into the terminal
            await PasteFromClipboardAsync(vm);
        }
    }

    /// <summary>
    /// Copies the current terminal selection to the clipboard and clears it.
    /// Returns false when nothing is selected, so Ctrl+C can fall through as an interrupt.
    /// </summary>
    private async Task<bool> TryCopySelectionAsync(TerminalPaneViewModel vm)
    {
        var hasSelection = vm.TerminalModel.HasSelection || Terminal.HasSelection;
        if (!hasSelection) return false;

        var rawText = !string.IsNullOrEmpty(Terminal.SelectedText) ? Terminal.SelectedText : vm.TerminalModel.SelectedText;
        var text = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
        if (string.IsNullOrEmpty(text)) return false;

        var clipboard = GetClipboard();
        if (clipboard != null)
        {
            await clipboard.SetTextAsync(text);
        }
        vm.TerminalModel.ClearSelection();
        return true;
    }

    /// <summary>
    /// Pastes clipboard text into the running shell session, respecting bracketed paste mode.
    /// </summary>
    private async Task PasteFromClipboardAsync(TerminalPaneViewModel vm)
    {
        var clipboard = GetClipboard();
        if (clipboard == null || !vm.IsRunning) return;

        var text = await clipboard.TryGetTextAsync();
        if (!string.IsNullOrEmpty(text))
        {
            vm.PasteText(text);
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Reflection access for Link Detection")]
    public static bool TryGetTerminalWordOrLine(
        TerminalControl terminal,
        TerminalPaneViewModel vm,
        Point pos,
        out string lineText,
        out int colIndex,
        out int row,
        out double charWidth,
        out double charHeight)
    {
        lineText = string.Empty;
        colIndex = 0;
        row = 0;
        charWidth = 0;
        charHeight = 0;

        try
        {
            var bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var terminalField = terminal.GetType().GetField("_terminal", bindingFlags);
            var termObj = terminalField?.GetValue(terminal);
            if (termObj == null) return false;

            var charWidthProp = termObj.GetType().GetProperty("CharWidth", bindingFlags);
            var charHeightProp = termObj.GetType().GetProperty("CharHeight", bindingFlags);

            charWidth = charWidthProp != null ? Convert.ToDouble(charWidthProp.GetValue(termObj)) : 0;
            charHeight = charHeightProp != null ? Convert.ToDouble(charHeightProp.GetValue(termObj)) : 0;

            if (charWidth <= 0 || charHeight <= 0) return false;

            colIndex = (int)(pos.X / charWidth);
            row = (int)(pos.Y / charHeight);

            var linesProp = termObj.GetType().GetProperty("Screen", bindingFlags);
            var screenObj = linesProp?.GetValue(termObj);

            if (screenObj is System.Collections.IEnumerable enumerable)
            {
                int currentRow = 0;
                foreach (var line in enumerable)
                {
                    if (currentRow == row)
                    {
                        lineText = line?.ToString() ?? string.Empty;
                        return true;
                    }
                    currentRow++;
                }
            }
        }
        catch { }

        return false;
    }
}
