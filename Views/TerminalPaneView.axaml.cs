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
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
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
        // 1. Prevent standalone modifier keys (Ctrl, Shift, Alt, Meta) from destroying active text selection
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt or Key.LWin or Key.RWin)
        {
            e.Handled = true;
            return;
        }

        if (DataContext is not TerminalPaneViewModel vm) return;

        var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;
        var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
        var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;

        // AltGr (e.g. AltGr+Q = '@' on German layouts) arrives as Ctrl+Alt. Flag it so the
        // terminal control's bogus Ctrl+<letter> control byte is dropped and only the text input passes.
        vm.Tab.IsAltGrActive = isCtrl && isAlt;

        // The terminal control marks such a KeyDown as handled, which suppresses the TextInput event.
        // Deliver the produced character (e.g. '@', '€', ']') ourselves.
        if (isCtrl && isAlt && !string.IsNullOrEmpty(e.KeySymbol) && !char.IsControl(e.KeySymbol[0]))
        {
            e.Handled = true;
            if (vm.IsRunning)
            {
                vm.SendInput(System.Text.Encoding.UTF8.GetBytes(e.KeySymbol));
            }
            return;
        }

        // Shift+Enter / Ctrl+Enter: Insert a newline (0x0A / LF) for multiline prompts (e.g. in GitHub Copilot CLI, REPLs, editors).
        // By default, terminals send 0x0D (CR / Enter) which triggers immediate command submission.
        // Sending 0x0A (Line Feed) instructs CLI text areas to insert a line break without submitting the prompt.
        if (e.Key is Key.Enter or Key.Return && (isShift || isCtrl) && !isAlt)
        {
            e.Handled = true;
            if (vm.IsRunning)
            {
                vm.SendInput([0x0A]);
            }
            return;
        }

        // PageUp / PageDown routing (REQ-TERM-003):
        // Note: Ctrl+PageUp/PageDown and Ctrl+Shift+PageUp/PageDown are handled by MainWindow for tab switching/reordering.
        // When not modified by Ctrl:
        // - Shift+PageUp / Shift+PageDown or Plain PageUp / PageDown in normal buffer: scroll the scrollback buffer.
        // - In Alternate Buffer (e.g. less, vim, nano, htop, mc) or with Alt: transmit VT PageUp/PageDown sequences to the shell.
        if (e.Key is Key.PageUp or Key.PageDown && !isCtrl)
        {
            var isAltBuffer = vm.TerminalModel.Terminal.IsAlternateBufferActive;

            if (isAltBuffer || isAlt)
            {
                if (e.Key == Key.PageUp)
                {
                    vm.SendPageUp(alt: isAlt);
                }
                else
                {
                    vm.SendPageDown(alt: isAlt);
                }
            }
            else
            {
                if (e.Key == Key.PageUp)
                {
                    vm.PageUp();
                }
                else
                {
                    vm.PageDown();
                }
            }
            e.Handled = true;
            return;
        }

        // Copy (Ctrl+C with selection, or Ctrl+Shift+C) and paste (Ctrl+V / Ctrl+Shift+V) via the system clipboard.
        if (isCtrl && !isAlt)
        {
            var hasSelection = vm.TerminalModel.HasSelection || Terminal.HasSelection;

            // Ctrl+Shift+C: Explicit copy shortcut (standard terminal practice)
            if (e.Key == Key.C && isShift)
            {
                e.Handled = true;
                _ = TryCopySelectionAsync(vm);
                return;
            }

            // Ctrl+C: Copy if text is selected; otherwise explicitly transmit SIGINT (0x03) to ConPTY.
            // Guarantees immediate, 100% reliable interrupt delivery even in raw mode / TUI apps like Copilot CLI.
            if (e.Key == Key.C && !isShift)
            {
                if (hasSelection)
                {
                    e.Handled = true;
                    _ = TryCopySelectionAsync(vm);
                    return;
                }
                else
                {
                    e.Handled = true;
                    vm.SendInput([0x03]);
                    return;
                }
            }

            if (e.Key == Key.V)
            {
                e.Handled = true;
                _ = PasteFromClipboardAsync(vm);
                return;
            }
        }

        // Shift+Insert: Classic terminal paste shortcut
        if (isShift && !isCtrl && !isAlt && e.Key == Key.Insert)
        {
            e.Handled = true;
            _ = PasteFromClipboardAsync(vm);
            return;
        }

        // 1. Split Pane Shortcuts (Alt + Shift + Right / V for Vertical; Alt + Shift + Down / H for Horizontal)
        if (isAlt && isShift && !isCtrl)
        {
            if (e.Key is Key.Right or Key.V)
            {
                vm.Tab.SplitRight();
                e.Handled = true;
                return;
            }
            if (e.Key is Key.Down or Key.H)
            {
                vm.Tab.SplitDown();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Z)
            {
                vm.Tab.ToggleZoom();
                e.Handled = true;
                return;
            }
        }

        // 2. Pane Navigation Shortcuts (Alt + ArrowKeys or F6 / Shift+F6)
        if (isAlt && !isShift && !isCtrl)
        {
            if (e.Key == Key.Left)
            {
                vm.Tab.NavigatePane(Models.PaneNavigationDirection.Left);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Right)
            {
                vm.Tab.NavigatePane(Models.PaneNavigationDirection.Right);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Up)
            {
                vm.Tab.NavigatePane(Models.PaneNavigationDirection.Up);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Down)
            {
                vm.Tab.NavigatePane(Models.PaneNavigationDirection.Down);
                e.Handled = true;
                return;
            }
        }

        if (e.Key == Key.F6)
        {
            if (isShift) vm.Tab.FocusPreviousPane();
            else vm.Tab.FocusNextPane();
            e.Handled = true;
            return;
        }

        // 3. Pane Resize (Ctrl + Alt + ArrowKeys)
        if (isCtrl && isAlt && !isShift)
        {
            if (e.Key is Key.Left or Key.Right or Key.Up or Key.Down)
            {
                var dir = e.Key switch
                {
                    Key.Left => Models.PaneNavigationDirection.Left,
                    Key.Right => Models.PaneNavigationDirection.Right,
                    Key.Up => Models.PaneNavigationDirection.Up,
                    _ => Models.PaneNavigationDirection.Down
                };
                vm.Tab.ResizeActivePane(dir);
                e.Handled = true;
                return;
            }
        }

        // 4. Close Active Pane (Ctrl + Shift + W)
        if (isCtrl && isShift && !isAlt && e.Key == Key.W)
        {
            if (vm.Tab.HasMultiplePanes)
            {
                vm.Tab.CloseActivePane();
                e.Handled = true;
                return;
            }
        }

        // 5. In-Terminal Search (Ctrl + Shift + F)
        if (isCtrl && isShift && !isAlt && e.Key == Key.F)
        {
            vm.OpenSearch();
            e.Handled = true;
            return;
        }
    }
}
