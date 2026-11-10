using System;
using System.ComponentModel;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;
using SvcSystems.UI.Terminal;

namespace MultiShell.Views;

public partial class TerminalPaneView : UserControl
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
    private TerminalPaneViewModel? _currentVm;
    private Border? _miniHeader;
    private Border? _searchOverlay;
    private TextBox? _searchInputBox;
    internal Border? _hoverLinkBorder;

    public TerminalPaneView()
    {
        InitializeComponent();
        _miniHeader = this.FindControl<Border>("MiniHeader");
        _searchOverlay = this.FindControl<Border>("SearchOverlay");
        _searchInputBox = this.FindControl<TextBox>("SearchInputBox");
        _searchInputBox?.AddHandler(InputElement.KeyDownEvent, OnSearchInputBoxKeyDown, RoutingStrategies.Tunnel);

        ConfigureOverlayScrollBar();

        Loaded += (_, _) =>
        {
            ConfigureOverlayScrollBar();
            Dispatcher.UIThread.Post(() =>
            {
                if (DataContext is TerminalPaneViewModel vm)
                {
                    if (vm.TerminalModel.Terminal.Cols > 0 && vm.TerminalModel.Terminal.Rows > 0)
                    {
                        vm.Resize(vm.TerminalModel.Terminal.Cols, vm.TerminalModel.Terminal.Rows);
                    }
                    if (vm.IsActive && !vm.IsSearchOpen)
                    {
                        Terminal.Focus();
                    }
                }
            });
        };

        Terminal.SizeChanged += (_, _) =>
        {
            ConfigureOverlayScrollBar();
            if (DataContext is TerminalPaneViewModel vm && vm.TerminalModel.Terminal.Cols > 0 && vm.TerminalModel.Terminal.Rows > 0)
            {
                vm.Resize(vm.TerminalModel.Terminal.Cols, vm.TerminalModel.Terminal.Rows);
            }
        };

        PointerEntered += (_, _) =>
        {
            if (_miniHeader != null)
            {
                _miniHeader.Opacity = 1.0;
            }
        };

        PointerExited += (_, _) =>
        {
            if (_miniHeader != null)
            {
                _miniHeader.Opacity = 0.0;
            }
        };

        PointerPressed += (_, e) =>
        {
            if (DataContext is TerminalPaneViewModel vm)
            {
                vm.Focus();
            }

            if (!IsInsideSearchOverlay(e.Source))
            {
                Terminal.Focus();
            }
        };

        GotFocus += (_, e) =>
        {
            if (DataContext is TerminalPaneViewModel vm && !vm.IsActive)
            {
                vm.Focus();
            }

            if (!IsInsideSearchOverlay(e.Source) && !ReferenceEquals(e.Source, Terminal))
            {
                Terminal.Focus();
            }
        };

        Terminal.AddHandler(InputElement.KeyDownEvent, OnTerminalKeyDown, RoutingStrategies.Tunnel);
        Terminal.AddHandler(InputElement.KeyUpEvent, OnTerminalKeyUp, RoutingStrategies.Tunnel, handledEventsToo: true);
        Terminal.LostFocus += (_, _) => ResetAltGrState();
        Terminal.AddHandler(InputElement.PointerWheelChangedEvent, OnTerminalPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        Terminal.AddHandler(InputElement.PointerMovedEvent, OnTerminalPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
        Terminal.AddHandler(InputElement.PointerPressedEvent, OnTerminalPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        Terminal.PointerExited += (_, _) =>
        {
            HideLinkHighlight();
            Terminal.Cursor = Cursor.Default;
        };
        DataContextChanged += OnDataContextChanged;
    }

    public void FocusTerminal()
    {
        Terminal.Focus();
    }

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
                Grid.SetColumn(scrollBar, 0);
                scrollBar.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right;
                scrollBar.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
                scrollBar.Margin = new Thickness(0, 0, 1, 0);
                scrollBar.Width = 8;
            }

            if (Terminal.ColumnDefinitions.Count > 1)
            {
                Terminal.ColumnDefinitions[1].Width = new GridLength(0);
            }
        }
        catch { }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachViewModel();

        if (DataContext is TerminalPaneViewModel vm)
        {
            AttachViewModel(vm);
        }
    }

    private void DetachViewModel()
    {
        if (_currentVm != null)
        {
            if (_propChangedHandler != null)
            {
                _currentVm.PropertyChanged -= _propChangedHandler;
                _propChangedHandler = null;
            }
            _currentVm.FocusSearchBoxRequested -= OnFocusSearchBoxRequested;
            _currentVm.FocusTerminalRequested -= OnFocusTerminalRequested;
            _currentVm = null;
        }
    }

    private void AttachViewModel(TerminalPaneViewModel vm)
    {
        if (ReferenceEquals(_currentVm, vm) && ReferenceEquals(Terminal.Model, vm.TerminalModel))
        {
            return;
        }

        DetachViewModel();

        _currentVm = vm;
        _currentVm.FocusSearchBoxRequested += OnFocusSearchBoxRequested;
        _currentVm.FocusTerminalRequested += OnFocusTerminalRequested;
        Terminal.Model = vm.TerminalModel;
        ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);

        _propChangedHandler = (_, args) =>
        {
            if (args.PropertyName == nameof(TerminalPaneViewModel.IsDarkTerminalTheme) ||
                args.PropertyName == nameof(TerminalPaneViewModel.TerminalBackgroundBrush) ||
                args.PropertyName == nameof(TerminalPaneViewModel.TerminalCaretBrush))
            {
                Dispatcher.UIThread.Post(() => ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm));
            }
            else if (args.PropertyName == nameof(TerminalPaneViewModel.TerminalFontSize) ||
                     args.PropertyName == nameof(TerminalPaneViewModel.TerminalFontFamily))
            {
                Dispatcher.UIThread.Post(() =>
                {
                    Terminal.FontSize = vm.TerminalFontSize;
                    Terminal.FontFamily = vm.TerminalFontFamily;
                    ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);
                });
            }
            else if (args.PropertyName == nameof(TerminalPaneViewModel.IsActive) && vm.IsActive)
            {
                Dispatcher.UIThread.Post(() => Terminal.Focus());
            }
        };

        vm.PropertyChanged += _propChangedHandler;
        vm.StartSession();
        if (vm.IsActive)
        {
            Dispatcher.UIThread.Post(() => Terminal.Focus());
        }
    }

    private void ApplyTerminalTheme(bool isDark, TerminalPaneViewModel vm)
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

        for (var i = 0; i < palette.Length; i++)
        {
            var key = $"SvcSystems.UI.TerminalColor{i}";
            Terminal.Resources[key] = palette[i];
            if (TerminalScope != null)
            {
                TerminalScope.Resources[key] = palette[i];
            }
        }

        try
        {
            var fallbackField = typeof(TerminalControl).GetField("FallbackXtermPalette", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (fallbackField?.GetValue(null) is Brush[] fallbackArray)
            {
                for (var i = 0; i < palette.Length && i < fallbackArray.Length; i++)
                {
                    fallbackArray[i] = palette[i];
                }
            }

            var clearCacheMethod = typeof(TerminalControl).GetMethod("ClearFormattedTextCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (clearCacheMethod != null)
            {
                clearCacheMethod.Invoke(Terminal, null);
            }
            else
            {
                var cacheField = typeof(TerminalControl).GetField("_formattedTextCache", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (cacheField?.GetValue(Terminal) is System.Collections.IDictionary cache)
                {
                    cache.Clear();
                }
            }
        }
        catch { }

        Terminal.InvalidateVisual();
        foreach (var child in Terminal.Children)
        {
            child.InvalidateVisual();
        }

        try
        {
            var surfaceField = typeof(TerminalControl).GetField("_surface", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (surfaceField?.GetValue(Terminal) is Control surface)
            {
                surface.InvalidateVisual();
            }
        }
        catch { }
    }


    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (DataContext is TerminalPaneViewModel vm)
        {
            if (_currentVm != vm)
            {
                AttachViewModel(vm);
            }
            else if (Terminal.Model == null)
            {
                Terminal.Model = vm.TerminalModel;
                ApplyTerminalTheme(vm.IsDarkTerminalTheme, vm);
            }
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        // Do NOT set Terminal.Model = null here!
        // SplitPaneContainerControl caches and re-parents TerminalPaneView instances during binary-tree splits.
        // Clearing Model on temporary detachment causes blank/empty terminal panes.
    }

    private void OnFocusSearchBoxRequested()
    {
        Dispatcher.UIThread.Post(() =>
        {
            _searchInputBox?.Focus();
            _searchInputBox?.SelectAll();
        });
    }

    private void OnFocusTerminalRequested()
    {
        Dispatcher.UIThread.Post(() => Terminal.Focus());
    }

    private bool IsInsideSearchOverlay(object? source)
    {
        if (_searchOverlay == null || source is not Visual visual) return false;
        Visual? current = visual;
        while (current != null)
        {
            if (ReferenceEquals(current, _searchOverlay)) return true;
            current = current.GetVisualParent();
        }
        return false;
    }

    private void OnSearchInputBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not TerminalPaneViewModel vm) return;

        if (e.Key == Key.Escape)
        {
            vm.CloseSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if ((e.KeyModifiers & KeyModifiers.Shift) != 0) vm.SearchPrevious();
            else vm.SearchNext();
            e.Handled = true;
        }
    }
}
