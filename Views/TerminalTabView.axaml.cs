using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
using MultiShell.Services;
using MultiShell.ViewModels;
using SvcSystems.UI.Terminal;

namespace MultiShell.Views;

public partial class TerminalTabView : UserControl
{
    private static readonly IBrush[] DarkPalette =
    [
        new ImmutableSolidColorBrush(Color.Parse("#0E0F15")), // 0: Dark Background
        new ImmutableSolidColorBrush(Color.Parse("#F7768E")), // 1: Red
        new ImmutableSolidColorBrush(Color.Parse("#9ECE6A")), // 2: Green
        new ImmutableSolidColorBrush(Color.Parse("#E0AF68")), // 3: Yellow
        new ImmutableSolidColorBrush(Color.Parse("#7AA2F7")), // 4: Blue
        new ImmutableSolidColorBrush(Color.Parse("#BB9AF7")), // 5: Magenta
        new ImmutableSolidColorBrush(Color.Parse("#7DCFFF")), // 6: Cyan
        new ImmutableSolidColorBrush(Color.Parse("#C0CAF5")), // 7: Light Foreground Text
        new ImmutableSolidColorBrush(Color.Parse("#565F89")), // 8: Bright Black / Muted
        new ImmutableSolidColorBrush(Color.Parse("#F7768E")), // 9: Bright Red
        new ImmutableSolidColorBrush(Color.Parse("#9ECE6A")), // 10: Bright Green
        new ImmutableSolidColorBrush(Color.Parse("#E0AF68")), // 11: Bright Yellow
        new ImmutableSolidColorBrush(Color.Parse("#7AA2F7")), // 12: Bright Blue
        new ImmutableSolidColorBrush(Color.Parse("#BB9AF7")), // 13: Bright Magenta
        new ImmutableSolidColorBrush(Color.Parse("#7DCFFF")), // 14: Bright Cyan
        new ImmutableSolidColorBrush(Color.Parse("#FFFFFF"))  // 15: Bright White
    ];

    private static readonly IBrush[] LightPalette =
    [
        new ImmutableSolidColorBrush(Color.Parse("#F8F9FC")), // 0: Light Background
        new ImmutableSolidColorBrush(Color.Parse("#D32F2F")), // 1: Red
        new ImmutableSolidColorBrush(Color.Parse("#2E7D32")), // 2: Green
        new ImmutableSolidColorBrush(Color.Parse("#E65100")), // 3: Dark Yellow / Orange
        new ImmutableSolidColorBrush(Color.Parse("#1976D2")), // 4: Blue
        new ImmutableSolidColorBrush(Color.Parse("#7B1FA2")), // 5: Magenta
        new ImmutableSolidColorBrush(Color.Parse("#0097A7")), // 6: Cyan
        new ImmutableSolidColorBrush(Color.Parse("#1A1D2B")), // 7: Dark Foreground Text
        new ImmutableSolidColorBrush(Color.Parse("#757D96")), // 8: Gray
        new ImmutableSolidColorBrush(Color.Parse("#C62828")), // 9: Bright Red
        new ImmutableSolidColorBrush(Color.Parse("#1B5E20")), // 10: Bright Green
        new ImmutableSolidColorBrush(Color.Parse("#BF360C")), // 11: Bright Yellow
        new ImmutableSolidColorBrush(Color.Parse("#0D47A1")), // 12: Bright Blue
        new ImmutableSolidColorBrush(Color.Parse("#4A148C")), // 13: Bright Magenta
        new ImmutableSolidColorBrush(Color.Parse("#006064")), // 14: Bright Cyan
        new ImmutableSolidColorBrush(Color.Parse("#0A0B10"))  // 15: Bright Black / Dark Text
    ];

    private PropertyChangedEventHandler? _propChangedHandler;
    private TerminalTabViewModel? _currentVm;
    private Border? _hoverLinkBorder;

    public TerminalTabView()
    {
        InitializeComponent();
        _hoverLinkBorder = this.FindControl<Border>("HoverLinkBorder");

        Loaded += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => Terminal.Focus());
        };

        PointerPressed += (_, _) =>
        {
            Terminal.Focus();
        };

        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
            {
                Dispatcher.UIThread.Post(() => Terminal.Focus());
            }
        };

        Terminal.AddHandler(InputElement.KeyDownEvent, OnTerminalKeyDown, RoutingStrategies.Tunnel);
        Terminal.AddHandler(InputElement.KeyUpEvent, OnTerminalKeyUp, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        Terminal.AddHandler(InputElement.PointerPressedEvent, OnTerminalPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Terminal.AddHandler(InputElement.PointerMovedEvent, OnTerminalPointerMoved, RoutingStrategies.Tunnel);
        Terminal.AddHandler(InputElement.PointerWheelChangedEvent, OnTerminalPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Terminal.PointerExited += (_, _) =>
        {
            HideLinkHighlight();
            Terminal.Cursor = Cursor.Default;
        };

        DataContextChanged += OnDataContextChanged;
    }

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
            var textSizeField = typeof(TerminalControl).GetField("_consoleTextSize", BindingFlags.NonPublic | BindingFlags.Instance);
            if (textSizeField?.GetValue(terminal) is Size size && size.Width > 0 && size.Height > 0)
            {
                charWidth = size.Width;
                charHeight = size.Height;
            }
            else
            {
                var glyph = new FormattedText("M", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(terminal.FontFamily), terminal.FontSize, Brushes.White);
                charWidth = glyph.WidthIncludingTrailingWhitespace;
                charHeight = glyph.Height;
            }

            int col = Math.Max(0, (int)(pos.X / charWidth));
            int r = Math.Max(0, (int)(pos.Y / charHeight));

            colIndex = col;
            row = r;

            var termObj = vm.TerminalModel.Terminal;
            var bufferProp = termObj.GetType().GetProperty("Buffer", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var bufferObj = bufferProp?.GetValue(termObj);

            if (bufferObj != null)
            {
                var getLineMethod = bufferObj.GetType().GetMethod("GetLine", [typeof(int)]);
                if (getLineMethod != null)
                {
                    var yDisp = Convert.ToInt32(bufferObj.GetType().GetProperty("YDisp")?.GetValue(bufferObj) ?? 0);
                    int absoluteY = r + yDisp;

                    var lineObj = getLineMethod.Invoke(bufferObj, new object[] { absoluteY });
                    if (lineObj != null)
                    {
                        var strMethod = lineObj.GetType().GetMethod("TranslateToString", [typeof(bool), typeof(int), typeof(int)]);
                        if (strMethod != null)
                        {
                            lineText = strMethod.Invoke(lineObj, new object[] { true, 0, vm.TerminalModel.Terminal.Cols })?.ToString() ?? string.Empty;
                            return !string.IsNullOrWhiteSpace(lineText);
                        }
                    }
                }
            }
        }
        catch
        {
            // Graceful fallback
        }

        return false;
    }

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

        if (isAltGr)
        {
            vm.IsAltGrActive = true;
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

        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
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

        // 3b. Ctrl+Shift+K: Clear terminal buffer and screen
        if (isCtrl && isShift && e.Key == Key.K)
        {
            vm.ClearBuffer();
            e.Handled = true;
            return;
        }

        // 3c. Ctrl+Shift+C: Copy selected text without sending interrupt signals
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

        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        var isAltGr = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Alt)) == (KeyModifiers.Control | KeyModifiers.Alt);
        if (DataContext is TerminalTabViewModel vm)
        {
            vm.IsAltGrActive = isAltGr;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null && _propChangedHandler != null)
        {
            _currentVm.PropertyChanged -= _propChangedHandler;
        }

        if (DataContext is TerminalTabViewModel vm)
        {
            _currentVm = vm;
            Terminal.Model = vm.TerminalModel;
            ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);

            _propChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(TerminalTabViewModel.IsDarkTerminalTheme) ||
                    args.PropertyName == nameof(TerminalTabViewModel.TerminalBackgroundBrush) ||
                    args.PropertyName == nameof(TerminalTabViewModel.TerminalCaretBrush))
                {
                    Dispatcher.UIThread.Post(() => ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm));
                }
                else if (args.PropertyName == nameof(TerminalTabViewModel.TerminalFontSize) ||
                         args.PropertyName == nameof(TerminalTabViewModel.TerminalFontFamily))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Terminal.FontSize = vm.TerminalFontSize;
                        Terminal.FontFamily = vm.TerminalFontFamily;
                        ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);
                    });
                }
                else if (args.PropertyName == nameof(TerminalTabViewModel.IsSelected) && vm.IsSelected)
                {
                    Dispatcher.UIThread.Post(() => Terminal.Focus());
                }
            };

            vm.PropertyChanged += _propChangedHandler;

            vm.StartSession();
            Dispatcher.UIThread.Post(() => Terminal.Focus());
        }
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "Best-effort reflection on third-party TerminalControl internal cache for runtime theme switching")]
    private void ApplyTerminalTheme(bool isDark, TerminalTabViewModel vm)
    {
        var palette = isDark ? DarkPalette : LightPalette;

        if (TerminalScope != null)
        {
            TerminalScope.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        Terminal.Background = palette[0];
        Terminal.CaretBrush = vm.TerminalCaretBrush;
        Terminal.SelectionBrush = isDark
            ? new ImmutableSolidColorBrush(Color.FromArgb(120, 122, 162, 247))
            : new ImmutableSolidColorBrush(Color.FromArgb(120, 25, 118, 210));

        // 1. Populate Terminal.Resources dictionary with explicit Xterm colors
        for (var i = 0; i < palette.Length; i++)
        {
            var key = $"SvcSystems.UI.TerminalColor{i}";
            Terminal.Resources[key] = palette[i];
        }

        try
        {
            // 2. Update the static FallbackXtermPalette array so ResolvePaletteBrush returns the exact theme colors
            var fallbackField = typeof(TerminalControl).GetField("FallbackXtermPalette", BindingFlags.NonPublic | BindingFlags.Static);
            if (fallbackField?.GetValue(null) is Brush[] fallbackArray)
            {
                for (var i = 0; i < palette.Length && i < fallbackArray.Length; i++)
                {
                    if (palette[i] is Brush b)
                    {
                        fallbackArray[i] = b;
                    }
                }
            }

            // 3. Clear cached formatted text so all character cells re-evaluate against the updated palette
            var cacheField = typeof(TerminalControl).GetField("_formattedTextCache", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cacheField?.GetValue(Terminal) is IDictionary cache)
            {
                cache.Clear();
            }

            var cacheOrderField = typeof(TerminalControl).GetField("_formattedTextCacheOrder", BindingFlags.NonPublic | BindingFlags.Instance);
            var cacheOrderObj = cacheOrderField?.GetValue(Terminal);
            if (cacheOrderObj is IList cacheOrderList)
            {
                cacheOrderList.Clear();
            }
            else if (cacheOrderObj is IDictionary cacheOrderDict)
            {
                cacheOrderDict.Clear();
            }
            else if (cacheOrderField != null && cacheOrderObj != null)
            {
                var clearMethod = cacheOrderField.FieldType.GetMethod("Clear", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                clearMethod?.Invoke(cacheOrderObj, null);
            }
        }
        catch
        {
            // Gracefully ignore reflection access if internal structure differs
        }

        // 4. Force redraw on both TerminalControl and its internal TerminalSurface canvas
        Terminal.InvalidateVisual();

        try
        {
            var surfaceField = typeof(TerminalControl).GetField("_surface", BindingFlags.NonPublic | BindingFlags.Instance);
            if (surfaceField?.GetValue(Terminal) is Control surface)
            {
                surface.InvalidateVisual();
            }
        }
        catch
        {
            // Gracefully ignore reflection access
        }
    }
}
