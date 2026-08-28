using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Styling;
using Avalonia.Threading;
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

    public TerminalTabView()
    {
        InitializeComponent();

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

        DataContextChanged += OnDataContextChanged;
    }

    private async void OnTerminalPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(Terminal);
        if (point.Properties.IsRightButtonPressed)
        {
            if (DataContext is TerminalTabViewModel vm)
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

        // 2. Ctrl+C with active selection -> Copy to clipboard and prevent sending \x03
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

        // 3. Ctrl+V -> Paste from clipboard into terminal
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

        // 4. Ctrl+Enter or Shift+Enter -> Send Linefeed (\n / 0x0A) for multi-line script continuation without executing command
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
