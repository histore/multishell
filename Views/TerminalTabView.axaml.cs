using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using MultiShell.ViewModels;
using SvcSystems.UI.Terminal;

namespace MultiShell.Views;

public partial class TerminalTabView : UserControl
{
    private static readonly SolidColorBrush[] DarkPalette =
    [
        new(Color.Parse("#0E0F15")), // 0: Dark Background
        new(Color.Parse("#F7768E")), // 1: Red
        new(Color.Parse("#9ECE6A")), // 2: Green
        new(Color.Parse("#E0AF68")), // 3: Yellow
        new(Color.Parse("#7AA2F7")), // 4: Blue
        new(Color.Parse("#BB9AF7")), // 5: Magenta
        new(Color.Parse("#7DCFFF")), // 6: Cyan
        new(Color.Parse("#C0CAF5")), // 7: Light Foreground Text
        new(Color.Parse("#565F89")), // 8: Bright Black / Muted
        new(Color.Parse("#F7768E")), // 9: Bright Red
        new(Color.Parse("#9ECE6A")), // 10: Bright Green
        new(Color.Parse("#E0AF68")), // 11: Bright Yellow
        new(Color.Parse("#7AA2F7")), // 12: Bright Blue
        new(Color.Parse("#BB9AF7")), // 13: Bright Magenta
        new(Color.Parse("#7DCFFF")), // 14: Bright Cyan
        new(Color.Parse("#FFFFFF"))  // 15: Bright White
    ];

    private static readonly SolidColorBrush[] LightPalette =
    [
        new(Color.Parse("#F8F9FC")), // 0: Light Background
        new(Color.Parse("#D32F2F")), // 1: Red
        new(Color.Parse("#2E7D32")), // 2: Green
        new(Color.Parse("#E65100")), // 3: Dark Yellow / Orange
        new(Color.Parse("#1976D2")), // 4: Blue
        new(Color.Parse("#7B1FA2")), // 5: Magenta
        new(Color.Parse("#0097A7")), // 6: Cyan
        new(Color.Parse("#1A1D2B")), // 7: Dark Foreground Text
        new(Color.Parse("#757D96")), // 8: Gray
        new(Color.Parse("#C62828")), // 9: Bright Red
        new(Color.Parse("#1B5E20")), // 10: Bright Green
        new(Color.Parse("#BF360C")), // 11: Bright Yellow
        new(Color.Parse("#0D47A1")), // 12: Bright Blue
        new(Color.Parse("#4A148C")), // 13: Bright Magenta
        new(Color.Parse("#006064")), // 14: Bright Cyan
        new(Color.Parse("#0A0B10"))  // 15: Bright Black / Dark Text
    ];

    private PropertyChangedEventHandler? _propChangedHandler;

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

        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is TerminalTabViewModel vm)
        {
            Terminal.Model = vm.TerminalModel;
            ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);

            if (_propChangedHandler != null)
            {
                vm.PropertyChanged -= _propChangedHandler;
            }

            _propChangedHandler = (_, args) =>
            {
                if (args.PropertyName == nameof(TerminalTabViewModel.IsDarkTerminalTheme) ||
                    args.PropertyName == nameof(TerminalTabViewModel.TerminalBackgroundBrush) ||
                    args.PropertyName == nameof(TerminalTabViewModel.TerminalCaretBrush))
                {
                    Dispatcher.UIThread.Post(() => ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm));
                }
                else if (args.PropertyName == nameof(TerminalTabViewModel.TerminalFontSize))
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        Terminal.FontSize = vm.TerminalFontSize;
                        ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);
                    });
                }
            };

            vm.PropertyChanged += _propChangedHandler;

            vm.StartSession();
            Dispatcher.UIThread.Post(() => Terminal.Focus());
        }
    }

    private void ApplyTerminalTheme(bool isDark, TerminalTabViewModel vm)
    {
        var palette = isDark ? DarkPalette : LightPalette;

        if (TerminalScope != null)
        {
            TerminalScope.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
        }

        Terminal.Background = palette[0];
        Terminal.CaretBrush = vm.TerminalCaretBrush;

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
                    fallbackArray[i] = palette[i];
                }
            }

            // 3. Clear cached formatted text so all character cells re-evaluate against the updated palette
            var cacheField = typeof(TerminalControl).GetField("_formattedTextCache", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cacheField?.GetValue(Terminal) is IDictionary cache)
            {
                cache.Clear();
            }

            var cacheOrderField = typeof(TerminalControl).GetField("_formattedTextCacheOrder", BindingFlags.NonPublic | BindingFlags.Instance);
            if (cacheOrderField?.GetValue(Terminal) is ICollection cacheOrder)
            {
                var clearMethod = cacheOrder.GetType().GetMethod("Clear");
                clearMethod?.Invoke(cacheOrder, null);
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
