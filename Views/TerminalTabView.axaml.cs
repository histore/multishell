using System;
using System.Collections;
using System.ComponentModel;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
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
        new SolidColorBrush(Color.Parse("#0E0F15")), // 0: Dark Background
        new SolidColorBrush(Color.Parse("#F7768E")), // 1: Red
        new SolidColorBrush(Color.Parse("#9ECE6A")), // 2: Green
        new SolidColorBrush(Color.Parse("#E0AF68")), // 3: Yellow
        new SolidColorBrush(Color.Parse("#7AA2F7")), // 4: Blue
        new SolidColorBrush(Color.Parse("#BB9AF7")), // 5: Magenta
        new SolidColorBrush(Color.Parse("#7DCFFF")), // 6: Cyan
        new SolidColorBrush(Color.Parse("#C0CAF5")), // 7: Light Foreground Text
        new SolidColorBrush(Color.Parse("#565F89")), // 8: Bright Black / Muted
        new SolidColorBrush(Color.Parse("#F7768E")), // 9: Bright Red
        new SolidColorBrush(Color.Parse("#9ECE6A")), // 10: Bright Green
        new SolidColorBrush(Color.Parse("#E0AF68")), // 11: Bright Yellow
        new SolidColorBrush(Color.Parse("#7AA2F7")), // 12: Bright Blue
        new SolidColorBrush(Color.Parse("#BB9AF7")), // 13: Bright Magenta
        new SolidColorBrush(Color.Parse("#7DCFFF")), // 14: Bright Cyan
        new SolidColorBrush(Color.Parse("#FFFFFF"))  // 15: Bright White
    ];

    private static readonly SolidColorBrush[] LightPalette =
    [
        new SolidColorBrush(Color.Parse("#F8F9FC")), // 0: Light Background
        new SolidColorBrush(Color.Parse("#D32F2F")), // 1: Red
        new SolidColorBrush(Color.Parse("#2E7D32")), // 2: Green
        new SolidColorBrush(Color.Parse("#E65100")), // 3: Dark Yellow / Orange
        new SolidColorBrush(Color.Parse("#1976D2")), // 4: Blue
        new SolidColorBrush(Color.Parse("#7B1FA2")), // 5: Magenta
        new SolidColorBrush(Color.Parse("#0097A7")), // 6: Cyan
        new SolidColorBrush(Color.Parse("#1A1D2B")), // 7: Dark Foreground Text
        new SolidColorBrush(Color.Parse("#757D96")), // 8: Gray
        new SolidColorBrush(Color.Parse("#C62828")), // 9: Bright Red
        new SolidColorBrush(Color.Parse("#1B5E20")), // 10: Bright Green
        new SolidColorBrush(Color.Parse("#BF360C")), // 11: Bright Yellow
        new SolidColorBrush(Color.Parse("#0D47A1")), // 12: Bright Blue
        new SolidColorBrush(Color.Parse("#4A148C")), // 13: Bright Magenta
        new SolidColorBrush(Color.Parse("#006064")), // 14: Bright Cyan
        new SolidColorBrush(Color.Parse("#0A0B10"))  // 15: Bright Black / Dark Text
    ];

    private PropertyChangedEventHandler? _propChangedHandler;
    private TerminalTabViewModel? _currentVm;
    private Border? _hoverLinkBorder;
    private Border? _searchOverlay;
    private TextBox? _searchInputBox;

    public TerminalTabView()
    {
        InitializeComponent();
        _hoverLinkBorder = this.FindControl<Border>("HoverLinkBorder");
        _searchOverlay = this.FindControl<Border>("SearchOverlay");
        _searchInputBox = this.FindControl<TextBox>("SearchInputBox");
        _searchInputBox?.AddHandler(InputElement.KeyDownEvent, OnSearchInputBoxKeyDown, RoutingStrategies.Tunnel);

        ConfigureOverlayScrollBar();

        Loaded += (_, _) =>
        {
            ConfigureOverlayScrollBar();
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is not TerminalTabViewModel vm || !vm.IsSearchOpen)
                {
                    Terminal.Focus();
                }
            });
        };

        PointerPressed += (_, e) =>
        {
            if (IsInsideSearchOverlay(e.Source))
            {
                return;
            }

            Terminal.Focus();
        };

        GotFocus += (_, e) =>
        {
            if (IsInsideSearchOverlay(e.Source))
            {
                return;
            }

            if (!ReferenceEquals(e.Source, Terminal))
            {
                Terminal.Focus();
            }
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

    /// <summary>
    /// Explicitly focuses the inner TerminalControl.
    /// </summary>
    public void FocusTerminal()
    {
        Terminal.Focus();
    }

    /// <summary>
    /// Decouples the vertical scrollbar from grid column sizing by converting it into an overlay scrollbar (REQ-TERM-011).
    /// Prevents TerminalSurface from losing width and resizing ConPTY when output reaches the bottom of the screen.
    /// </summary>
    private void ConfigureOverlayScrollBar()
    {
        try
        {
            Avalonia.Controls.Primitives.ScrollBar? scrollBar = null;
            foreach (var child in Terminal.Children)
            {
                if (child is Avalonia.Controls.Primitives.ScrollBar sb)
                {
                    scrollBar = sb;
                    break;
                }
            }

            if (scrollBar != null)
            {
                // Move scrollbar to column 0 so it floats as an overlay on top of the terminal surface
                Grid.SetColumn(scrollBar, 0);
                scrollBar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
                scrollBar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                scrollBar.Margin = new Thickness(0, 0, 1, 0);
                scrollBar.Width = 10;
            }

            if (Terminal.ColumnDefinitions.Count > 1)
            {
                Terminal.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }
        catch
        {
            // Gracefully ignore layout adjustments if internal structure differs
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_currentVm != null)
        {
            if (_propChangedHandler != null)
            {
                _currentVm.PropertyChanged -= _propChangedHandler;
            }
            _currentVm.FocusSearchBoxRequested -= OnFocusSearchBoxRequested;
            _currentVm.FocusTerminalRequested -= OnFocusTerminalRequested;
        }

        if (DataContext is TerminalTabViewModel vm)
        {
            _currentVm = vm;
            _currentVm.FocusSearchBoxRequested += OnFocusSearchBoxRequested;
            _currentVm.FocusTerminalRequested += OnFocusTerminalRequested;
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
            ? new SolidColorBrush(Color.FromArgb(120, 122, 162, 247))
            : new SolidColorBrush(Color.FromArgb(120, 25, 118, 210));

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
            var clearCacheMethod = typeof(TerminalControl).GetMethod("ClearFormattedTextCache", BindingFlags.NonPublic | BindingFlags.Instance);
            if (clearCacheMethod != null)
            {
                clearCacheMethod.Invoke(Terminal, null);
            }
            else
            {
                var cacheField = typeof(TerminalControl).GetField("_formattedTextCache", BindingFlags.NonPublic | BindingFlags.Instance);
                if (cacheField?.GetValue(Terminal) is IDictionary cache)
                {
                    cache.Clear();
                }
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
