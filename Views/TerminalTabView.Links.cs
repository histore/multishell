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
using SvcSystems.UI.Terminal;

namespace MultiShell.Views;

public partial class TerminalTabView
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

    private void OnTerminalPointerMoved(object? sender, PointerEventArgs e)
    {
        if (DataContext is not TerminalTabViewModel vm) return;

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
        if (e.Handled) return;
        var point = e.GetCurrentPoint(Terminal);
        if (DataContext is not TerminalTabViewModel vm) return;

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
                else if (vm.TerminalModel.HasSelection)
                {
                    var selected = vm.TerminalModel.SelectedText.Trim();
                    if (LinkDetectionHelper.OpenTarget(selected, vm.WorkingDirectory))
                    {
                        e.Handled = true;
                        return;
                    }
                }
            }
        }

        // 2. Right-Click: Copy selection or Paste clipboard
        if (point.Properties.IsRightButtonPressed)
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;

            if (vm.TerminalModel.HasSelection)
            {
                // Copy selection to clipboard and clear selection
                var rawText = vm.TerminalModel.SelectedText;
                var text = TerminalTabViewModel.CleanSelectedTerminalText(rawText);
                if (!string.IsNullOrEmpty(text) && clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                }
                vm.TerminalModel.ClearSelection();
                Terminal.InvalidateVisual();
                e.Handled = true;
            }
            else
            {
                // No selection: paste clipboard content into terminal
                if (clipboard != null)
                {
                    var text = await clipboard.TryGetTextAsync();
                    if (!string.IsNullOrEmpty(text) && vm.IsRunning)
                    {
                        vm.SendInput(Encoding.UTF8.GetBytes(text));
                    }
                }
                e.Handled = true;
            }
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Best-effort reflection on third-party TerminalControl buffer with graceful fallback for clickable hyperlinks")]
    public static bool TryGetTerminalWordOrLine(TerminalControl terminal, TerminalTabViewModel vm, Point pos, out string lineText, out int colIndex, out int row, out double charWidth, out double charHeight)
    {
        lineText = string.Empty;
        colIndex = 0;
        row = 0;
        charWidth = 8.0;
        charHeight = 16.0;

        if (vm.TerminalModel.HasSelection && !string.IsNullOrWhiteSpace(vm.TerminalModel.SelectedText))
        {
            lineText = vm.TerminalModel.SelectedText.Trim();
            colIndex = 0;
            return true;
        }

        try
        {
            var charSize = ResolveCharacterSize(terminal);
            charWidth = charSize.Width;
            charHeight = charSize.Height;

            int col = Math.Max(0, (int)(pos.X / charWidth));
            int r = Math.Max(0, (int)(pos.Y / charHeight));
            colIndex = col;
            row = r;

            var termObj = vm.TerminalModel.Terminal;
            var bufferProp = termObj.GetType().GetProperty("Buffer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bufferObj = bufferProp?.GetValue(termObj);
            if (bufferObj is null) return false;

            var getLineMethod = bufferObj.GetType().GetMethod("GetLine", [typeof(int)]);
            if (getLineMethod is null) return false;

            var yDisp = Convert.ToInt32(bufferObj.GetType().GetProperty("YDisp")?.GetValue(bufferObj) ?? 0);
            int absoluteY = r + yDisp;

            var lineObj = getLineMethod.Invoke(bufferObj, [absoluteY]);
            if (lineObj is null) return false;

            var strMethod = lineObj.GetType().GetMethod("TranslateToString", [typeof(bool), typeof(int), typeof(int)]);
            if (strMethod is null) return false;

            lineText = strMethod.Invoke(lineObj, [true, 0, vm.TerminalModel.Terminal.Cols])?.ToString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(lineText);
        }
        catch
        {
            // Graceful fallback if terminal internals differ or are trimmed
            return false;
        }
    }

    private static Size ResolveCharacterSize(TerminalControl terminal)
    {
        var textSizeField = typeof(TerminalControl).GetField("_consoleTextSize", BindingFlags.NonPublic | BindingFlags.Instance);
        if (textSizeField?.GetValue(terminal) is Size size && size.Width > 0 && size.Height > 0)
        {
            return size;
        }

        var glyph = new FormattedText("M", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(terminal.FontFamily), terminal.FontSize, Brushes.White);
        return new Size(glyph.WidthIncludingTrailingWhitespace, glyph.Height);
    }
}
