using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow : Window
{
    private TerminalTabViewModel? _draggedTab;
    private Point _dragStartPos;
    private bool _isDragging;
    private double _tabWheelAccumulator;

    public MainWindow()
    {
        InitializeComponent();

        if (TabsItemsControl != null)
        {
            TabsItemsControl.AddHandler(InputElement.PointerPressedEvent, OnTabsPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.PointerMovedEvent, OnTabsPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.PointerReleasedEvent, OnTabsPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.PointerCaptureLostEvent, OnTabsPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        if (TabBarContainer != null)
        {
            TabBarContainer.AddHandler(InputElement.PointerWheelChangedEvent, OnTabsPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }

        if (TabsScrollViewer != null)
        {
            TabsScrollViewer.ScrollChanged += (_, _) => UpdateTabOverflowIndicators();
            TabsScrollViewer.SizeChanged += (_, _) => UpdateTabOverflowIndicators();
            TabsScrollViewer.AddHandler(InputElement.PointerWheelChangedEvent, OnTabsPointerWheelChanged, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        }

        if (TabScrollLeftBtn != null)
        {
            TabScrollLeftBtn.Click += (_, _) => ScrollTabsBy(-160);
        }

        if (TabScrollRightBtn != null)
        {
            TabScrollRightBtn.Click += (_, _) => ScrollTabsBy(160);
        }

        if (TabListMenuButton != null)
        {
            TabListMenuButton.AddHandler(Button.ClickEvent, OnTabMenuButtonClick, RoutingStrategies.Bubble);
        }

        // Toolbar Action Buttons
        if (ToggleHistoryToolbarBtn != null)
        {
            ToggleHistoryToolbarBtn.Click += (_, _) => ToggleHistoryDrawer();
        }

        if (SettingsProfilesMenuItem != null)
        {
            SettingsProfilesMenuItem.Click += (_, _) =>
            {
                SettingsToolbarBtn?.Flyout?.Hide();
                if (DataContext is MainViewModel vm)
                {
                    vm.OpenProfilesModal();
                }
            };
        }

        if (SettingsHelpMenuItem != null)
        {
            SettingsHelpMenuItem.Click += (_, _) =>
            {
                SettingsToolbarBtn?.Flyout?.Hide();
                ShowHelpModal();
            };
        }

        if (SettingsAboutMenuItem != null)
        {
            SettingsAboutMenuItem.Click += (_, _) =>
            {
                SettingsToolbarBtn?.Flyout?.Hide();
                ShowAboutModal();
            };
        }

        // Modals
        if (CloseHelpModalButton != null) CloseHelpModalButton.Click += (_, _) => HideHelpModal();
        if (OkHelpModalButton != null) OkHelpModalButton.Click += (_, _) => HideHelpModal();
        if (HelpModal != null) HelpModal.PointerPressed += (_, e) => { if (e.Source == HelpModal) HideHelpModal(); };

        if (OkAboutModalButton != null) OkAboutModalButton.Click += (_, _) => HideAboutModal();
        if (AboutModal != null) AboutModal.PointerPressed += (_, e) => { if (e.Source == AboutModal) HideAboutModal(); };

        if (ProfilesModal != null) ProfilesModal.PointerPressed += (_, e) => { if (e.Source == ProfilesModal && DataContext is MainViewModel vm) vm.CloseProfilesModal(); };

        // History Drawer
        if (HistoryHoverTrigger != null)
        {
            HistoryHoverTrigger.PointerEntered += (_, _) => ShowHistoryDrawer();
        }

        if (HistoryDrawer != null)
        {
            HistoryDrawer.PointerExited += (_, e) =>
            {
                var pos = e.GetPosition(HistoryDrawer);
                if (pos.X < 0 || pos.X >= HistoryDrawer.Bounds.Width || pos.Y < 0 || pos.Y >= HistoryDrawer.Bounds.Height)
                {
                    HideHistoryDrawerAndFocusTerminal();
                }
            };
        }

                if (ClearCommandFilterBtn != null)
        {
            ClearCommandFilterBtn.Click += (_, _) =>
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    vm.SelectedTab.CommandFilterQuery = string.Empty;
                }
                CommandHistorySearchBox?.Focus();
            };
        }

        if (ClearDirectoryFilterBtn != null)
        {
            ClearDirectoryFilterBtn.Click += (_, _) =>
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    vm.SelectedTab.DirectoryFilterQuery = string.Empty;
                }
                DirectoryHistorySearchBox?.Focus();
            };
        }

        if (CommandHistorySearchBox != null)
        {
            CommandHistorySearchBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    var filterText = CommandHistorySearchBox.Text;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (CommandHistoryListBox != null && CommandHistoryListBox.ItemCount > 0)
                        {
                            CommandHistoryListBox.SelectedIndex = !string.IsNullOrWhiteSpace(filterText)
                                ? 0
                                : CommandHistoryListBox.ItemCount - 1;

                            if (CommandHistoryListBox.SelectedItem != null)
                            {
                                CommandHistoryListBox.ScrollIntoView(CommandHistoryListBox.SelectedItem);
                            }
                        }
                    }, DispatcherPriority.Input);
                }
            };
        }

        if (DirectoryHistorySearchBox != null)
        {
            DirectoryHistorySearchBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    var filterText = DirectoryHistorySearchBox.Text;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (DirectoryHistoryListBox != null && DirectoryHistoryListBox.ItemCount > 0)
                        {
                            DirectoryHistoryListBox.SelectedIndex = !string.IsNullOrWhiteSpace(filterText)
                                ? 0
                                : DirectoryHistoryListBox.ItemCount - 1;

                            if (DirectoryHistoryListBox.SelectedItem != null)
                            {
                                DirectoryHistoryListBox.ScrollIntoView(DirectoryHistoryListBox.SelectedItem);
                            }
                        }
                    }, DispatcherPriority.Input);
                }
            };
        }

        if (CloseHistoryButton != null)
        {
            CloseHistoryButton.Click += (_, _) => HideHistoryDrawerAndFocusTerminal();
        }

        if (HistoryBackdrop != null)
        {
            HistoryBackdrop.PointerPressed += (_, _) => HideHistoryDrawerAndFocusTerminal();
        }

        // History ListBox Selection & Keyboard Execution
        if (CommandHistoryListBox != null)
        {
            CommandHistoryListBox.KeyDown += OnHistoryListBoxKeyDown;
            CommandHistoryListBox.AddHandler(InputElement.PointerPressedEvent, OnHistoryListBoxPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        if (DirectoryHistoryListBox != null)
        {
            DirectoryHistoryListBox.KeyDown += OnHistoryListBoxKeyDown;
            DirectoryHistoryListBox.AddHandler(InputElement.PointerPressedEvent, OnHistoryListBoxPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        if (HistoryTabControl != null)
        {
            HistoryTabControl.SelectionChanged += (sender, e) =>
            {
                // Only react if the TabControl itself changed tabs, not child ListBoxes bubbling SelectionChanged
                if (e.Source == HistoryTabControl)
                {
                    var hasFilter = false;
                    if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                    {
                        hasFilter = HistoryTabControl.SelectedIndex == 1
                            ? !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery)
                            : !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
                    }
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
            };
        }

        if (TabSwitcherOverlay != null)
        {
            TabSwitcherOverlay.PointerPressed += (_, e) =>
            {
                if (e.Source == TabSwitcherOverlay && DataContext is MainViewModel vm)
                {
                    vm.CancelTabSwitcher();
                    FocusActiveTerminal();
                }
            };
        }

        if (TabSwitcherListBox != null)
        {
            TabSwitcherListBox.AddHandler(InputElement.PointerPressedEvent, OnTabSwitcherListBoxPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        // Window-level Keyboard Filter
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);

        DataContextChanged += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(MainViewModel.SelectedTab))
                    {
                        ScrollSelectedTabIntoView();
                    }
                };
                vm.Tabs.CollectionChanged += (_, _) =>
                {
                    Dispatcher.UIThread.Post(() =>
                    {
                        UpdateTabOverflowIndicators();
                        ScrollSelectedTabIntoView();
                    }, DispatcherPriority.Loaded);
                };
            }
        };

        Closing += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.SaveCurrentStateSynchronously();
            }
        };

        Closed += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Dispose();
            }
        };
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Tab Switcher Active Keyboard Handling (REQ-TAB-019)
        if (DataContext is MainViewModel switcherVm && switcherVm.IsTabSwitcherOpen)
        {
            var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

            if (e.Key == Key.Tab || e.Key == Key.PageDown || e.Key == Key.Down)
            {
                switcherVm.AdvanceTabSwitcher(forward: !isShift);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.PageUp || e.Key == Key.Up)
            {
                switcherVm.AdvanceTabSwitcher(forward: false);
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Enter)
            {
                switcherVm.CommitTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
            if (e.Key == Key.Escape)
            {
                switcherVm.CancelTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
        }

        // Ctrl+Shift+H: Toggle History Drawer
        if (e.Key == Key.H && (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == (KeyModifiers.Control | KeyModifiers.Shift))
        {
            ToggleHistoryDrawer();
            e.Handled = true;
            return;
        }

        // When History Drawer is open, capture all navigation keys globally
        if (HistoryDrawer?.IsVisible == true)
        {
            if (e.Key == Key.Escape)
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    if (HistoryTabControl?.SelectedIndex == 1 && !string.IsNullOrEmpty(vm.SelectedTab.DirectoryFilterQuery))
                    {
                        vm.SelectedTab.DirectoryFilterQuery = string.Empty;
                        e.Handled = true;
                        return;
                    }
                    if (HistoryTabControl?.SelectedIndex == 0 && !string.IsNullOrEmpty(vm.SelectedTab.CommandFilterQuery))
                    {
                        vm.SelectedTab.CommandFilterQuery = string.Empty;
                        e.Handled = true;
                        return;
                    }
                }
                HideHistoryDrawerAndFocusTerminal();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter)
            {
                if (HistoryTabControl?.SelectedIndex == 1)
                {
                    PasteSelectedDirectory();
                }
                else
                {
                    PasteSelectedCommand();
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                NavigateHistorySelection(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                NavigateHistorySelection(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Left)
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = 0;
                    var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Right)
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = 1;
                    var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery);
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Tab)
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = HistoryTabControl.SelectedIndex == 0 ? 1 : 0;
                    var hasFilter = false;
                    if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                    {
                        hasFilter = HistoryTabControl.SelectedIndex == 1
                            ? !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery)
                            : !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
                    }
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
                e.Handled = true;
                return;
            }
        }

        // F1: Help Modal
        if (e.Key == Key.F1)
        {
            ShowHelpModal();
            e.Handled = true;
            return;
        }

        // Escape: Close active dialog or drawer
        if (e.Key == Key.Escape)
        {
            if (DataContext is MainViewModel dvm && dvm.IsTabSwitcherOpen)
            {
                dvm.CancelTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
                return;
            }
            if (HelpModal?.IsVisible == true)
            {
                HideHelpModal();
                e.Handled = true;
                return;
            }
            if (AboutModal?.IsVisible == true)
            {
                HideAboutModal();
                e.Handled = true;
                return;
            }
            if (ProfilesModal?.IsVisible == true && DataContext is MainViewModel pvm)
            {
                pvm.CloseProfilesModal();
                e.Handled = true;
                return;
            }
            if (HistoryDrawer?.IsVisible == true)
            {
                HideHistoryDrawerAndFocusTerminal();
                e.Handled = true;
                return;
            }
        }

        // Only process Tab management shortcuts if modal dialogs are not capturing input
        if (HelpModal?.IsVisible != true && AboutModal?.IsVisible != true && ProfilesModal?.IsVisible != true)
        {
            if (DataContext is MainViewModel vm)
            {
                var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
                var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;

                // 1. Ctrl+Shift+PageUp / Ctrl+Shift+PageDown: Move Tab Left/Right
                if (isCtrl && isShift && e.Key == Key.PageUp)
                {
                    vm.MoveSelectedTab(-1);
                    e.Handled = true;
                    return;
                }
                if (isCtrl && isShift && e.Key == Key.PageDown)
                {
                    vm.MoveSelectedTab(1);
                    e.Handled = true;
                    return;
                }

                // 2. Ctrl+Shift+W: Close active tab
                if (isCtrl && isShift && e.Key == Key.W)
                {
                    vm.CloseSelectedTab();
                    e.Handled = true;
                    return;
                }

                // 3. Ctrl+Tab / Ctrl+Shift+Tab: Unified Tab Switcher HUD (REQ-TAB-019)
                if (isCtrl && e.Key == Key.Tab)
                {
                    vm.ShowTabSwitcher(forward: !isShift, isKeyboardTriggered: true);
                    e.Handled = true;
                    return;
                }

                // 4. Ctrl+PageUp / Ctrl+PageDown: Previous / Next Tab
                if (isCtrl && !isShift && e.Key == Key.PageUp)
                {
                    vm.CyclePreviousTab();
                    e.Handled = true;
                    return;
                }
                if (isCtrl && !isShift && e.Key == Key.PageDown)
                {
                    vm.CycleNextTab();
                    e.Handled = true;
                    return;
                }

                // 5. Ctrl+1 .. Ctrl+8: Jump to specific tab index (0..7)
                if (isCtrl && !isShift)
                {
                    if (e.Key >= Key.D1 && e.Key <= Key.D8)
                    {
                        vm.SelectTabByIndex(e.Key - Key.D1);
                        e.Handled = true;
                        return;
                    }
                    if (e.Key >= Key.NumPad1 && e.Key <= Key.NumPad8)
                    {
                        vm.SelectTabByIndex(e.Key - Key.NumPad1);
                        e.Handled = true;
                        return;
                    }
                    // 6. Ctrl+9: Jump to last tab
                    if (e.Key is Key.D9 or Key.NumPad9)
                    {
                        vm.SelectTabByIndex(-1);
                        e.Handled = true;
                        return;
                    }

                    // 7. Zoom & Font-Size Shortcuts (REQ-UI-005)
                    if (e.Key is Key.OemPlus or Key.Add)
                    {
                        vm.IncreaseTerminalFontSize();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key is Key.OemMinus or Key.Subtract)
                    {
                        vm.DecreaseTerminalFontSize();
                        e.Handled = true;
                        return;
                    }
                    if (e.Key is Key.D0 or Key.NumPad0)
                    {
                        vm.ResetTerminalFontSize();
                        e.Handled = true;
                        return;
                    }
                }
            }
        }
    }

    private void OnWindowKeyUp(object? sender, KeyEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.IsTabSwitcherOpen && vm.TabSwitcherIsKeyboardTriggered)
        {
            // When Ctrl key is released (or Ctrl is no longer present in modifier flags), commit tab switch
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl || (e.KeyModifiers & KeyModifiers.Control) == 0)
            {
                vm.CommitTabSwitcher();
                FocusActiveTerminal();
                e.Handled = true;
            }
        }
    }

    public void ToggleHistoryDrawer()
    {
        if (HistoryDrawer == null) return;

        if (HistoryDrawer.IsVisible)
        {
            HideHistoryDrawerAndFocusTerminal();
        }
        else
        {
            ShowHistoryDrawer();
        }
    }

    public void ShowHistoryDrawer()
    {
        if (HistoryDrawer == null) return;
        HistoryDrawer.IsVisible = true;
        var hasFilter = false;
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            hasFilter = HistoryTabControl?.SelectedIndex == 1
                ? !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery)
                : !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
        }
        FocusActiveHistoryList(selectLastItem: !hasFilter);
    }

    private void FocusActiveHistoryList(bool selectLastItem = true)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var isDirTab = HistoryTabControl?.SelectedIndex == 1;
            var activeListBox = isDirTab ? DirectoryHistoryListBox : CommandHistoryListBox;
            var activeSearchBox = isDirTab ? DirectoryHistorySearchBox : CommandHistorySearchBox;

            if (activeListBox != null && activeListBox.ItemCount > 0)
            {
                if (selectLastItem)
                {
                    activeListBox.SelectedIndex = activeListBox.ItemCount - 1;
                }
                else
                {
                    activeListBox.SelectedIndex = 0;
                }

                if (activeListBox.SelectedItem != null)
                {
                    activeListBox.ScrollIntoView(activeListBox.SelectedItem);
                }
            }

            activeSearchBox?.Focus();
        }, DispatcherPriority.Input);
    }

    private void NavigateHistorySelection(int delta)
    {
        var activeListBox = HistoryTabControl?.SelectedIndex == 1
            ? DirectoryHistoryListBox
            : CommandHistoryListBox;

        if (activeListBox == null || activeListBox.ItemCount == 0) return;

        int currentIndex = activeListBox.SelectedIndex;
        if (currentIndex < 0)
        {
            currentIndex = delta < 0 ? activeListBox.ItemCount - 1 : 0;
        }
        else
        {
            currentIndex = Math.Clamp(currentIndex + delta, 0, activeListBox.ItemCount - 1);
        }

        activeListBox.SelectedIndex = currentIndex;
        if (activeListBox.SelectedItem != null)
        {
            activeListBox.ScrollIntoView(activeListBox.SelectedItem);
        }
    }

    private void OnHistoryListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
        {
            // Left click: paste into prompt without executing
            var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
            if (string.IsNullOrWhiteSpace(clickedItem)) return;

            e.Handled = true;
            if (sender == CommandHistoryListBox)
                PasteSelectedCommand(clickedItem);
            else if (sender == DirectoryHistoryListBox)
                PasteSelectedDirectory(clickedItem);
        }
        else if (props.IsRightButtonPressed)
        {
            // Right click: execute directly
            var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
            if (string.IsNullOrWhiteSpace(clickedItem)) return;

            e.Handled = true;
            if (sender == CommandHistoryListBox)
                ExecuteSelectedCommand(clickedItem);
            else if (sender == DirectoryHistoryListBox)
                ExecuteSelectedDirectory(clickedItem);
        }
    }

    private static string? GetHistoryItemFromPointerSource(Visual? visual)
    {
        while (visual != null)
        {
            if (visual.DataContext is string str && !string.IsNullOrWhiteSpace(str))
            {
                return str;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void OnTabSwitcherListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed)
        {
            var clickedTab = GetTabItemFromPointerSource(e.Source as Visual);
            if (clickedTab != null && DataContext is MainViewModel vm)
            {
                e.Handled = true;
                vm.SelectTabFromSwitcher(clickedTab);
                FocusActiveTerminal();
            }
        }
    }

    private static TerminalTabViewModel? GetTabItemFromPointerSource(Visual? visual)
    {
        while (visual != null)
        {
            if (visual.DataContext is TerminalTabViewModel tab)
            {
                return tab;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private void OnHistoryListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (sender == CommandHistoryListBox || HistoryTabControl?.SelectedIndex == 0)
            {
                PasteSelectedCommand();
            }
            else
            {
                PasteSelectedDirectory();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Left && HistoryTabControl != null)
        {
            HistoryTabControl.SelectedIndex = 0;
            var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery);
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && HistoryTabControl != null)
        {
            HistoryTabControl.SelectedIndex = 1;
            var hasFilter = DataContext is MainViewModel vm && vm.SelectedTab != null && !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery);
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideHistoryDrawerAndFocusTerminal();
            e.Handled = true;
        }
    }

    private void PasteSelectedCommand(string? explicitCommand = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var cmd = explicitCommand ?? CommandHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(cmd))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery) && vm.SelectedTab.FilteredCommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.FilteredCommandHistory[0];
                }
                else if (vm.SelectedTab.CommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.CommandHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                vm.SelectedTab.PasteHistoryCommand(cmd);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void PasteSelectedDirectory(string? explicitDirectory = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var dir = explicitDirectory ?? DirectoryHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(dir))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery) && vm.SelectedTab.FilteredDirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.FilteredDirectoryHistory[0];
                }
                else if (vm.SelectedTab.DirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.DirectoryHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(dir))
            {
                vm.SelectedTab.PasteHistoryDirectory(dir);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void ExecuteSelectedCommand(string? explicitCommand = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var cmd = explicitCommand ?? CommandHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(cmd))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery) && vm.SelectedTab.FilteredCommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.FilteredCommandHistory[0];
                }
                else if (vm.SelectedTab.CommandHistory.Count > 0)
                {
                    cmd = vm.SelectedTab.CommandHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(cmd))
            {
                vm.SelectedTab.ExecuteHistoryCommand(cmd);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    private void ExecuteSelectedDirectory(string? explicitDirectory = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var dir = explicitDirectory ?? DirectoryHistoryListBox?.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(dir))
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery) && vm.SelectedTab.FilteredDirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.FilteredDirectoryHistory[0];
                }
                else if (vm.SelectedTab.DirectoryHistory.Count > 0)
                {
                    dir = vm.SelectedTab.DirectoryHistory[^1];
                }
            }
            if (!string.IsNullOrWhiteSpace(dir))
            {
                vm.SelectedTab.NavigateToHistoryDirectory(dir);
            }
            HideHistoryDrawerAndFocusTerminal();
        }
    }

    public void ShowHelpModal()
    {
        if (HelpModal != null) HelpModal.IsVisible = true;
    }

    public void HideHelpModal()
    {
        if (HelpModal != null) HelpModal.IsVisible = false;
        FocusActiveTerminal();
    }

    public void ShowAboutModal()
    {
        if (AboutModal != null) AboutModal.IsVisible = true;
    }

    public void HideAboutModal()
    {
        if (AboutModal != null) AboutModal.IsVisible = false;
        FocusActiveTerminal();
    }

    private void HideHistoryDrawerAndFocusTerminal()
    {
        if (HistoryDrawer != null)
        {
            HistoryDrawer.IsVisible = false;
        }
        FocusActiveTerminal();
    }

    private void FocusActiveTerminal()
    {
        Dispatcher.UIThread.Post(() =>
        {
            var terminal = TabContentControl?.FindDescendantOfType<SvcSystems.UI.Terminal.TerminalControl>();
            if (terminal != null)
            {
                terminal.Focus();
            }
            else
            {
                TabContentControl?.Focus();
            }
        }, DispatcherPriority.Input);
    }

    private void OnTabMenuButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button btn && btn.Classes.Contains("tabMenuItemBtn"))
        {
            if (TabListMenuButton?.Flyout is Flyout flyout)
            {
                flyout.Hide();
            }
            ScrollSelectedTabIntoView();
        }
    }

    private void UpdateTabOverflowIndicators()
    {
        if (TabsScrollViewer == null) return;

        var extent = TabsScrollViewer.Extent.Width;
        var viewport = TabsScrollViewer.Viewport.Width;
        var offset = TabsScrollViewer.Offset.X;

        bool hasOverflow = extent > viewport + 1;
        bool canScrollLeft = offset > 1;
        bool canScrollRight = extent - (offset + viewport) > 1;

        if (TabScrollLeftBtn != null)
        {
            TabScrollLeftBtn.IsVisible = hasOverflow;
            TabScrollLeftBtn.IsEnabled = canScrollLeft;
            TabScrollLeftBtn.Opacity = canScrollLeft ? 1.0 : 0.4;
        }

        if (TabScrollRightBtn != null)
        {
            TabScrollRightBtn.IsVisible = hasOverflow;
            TabScrollRightBtn.IsEnabled = canScrollRight;
            TabScrollRightBtn.Opacity = canScrollRight ? 1.0 : 0.4;
        }

        if (LeftEdgeFade != null)
        {
            LeftEdgeFade.IsVisible = canScrollLeft;
        }

        if (RightEdgeFade != null)
        {
            RightEdgeFade.IsVisible = canScrollRight;
        }
    }

    private void ScrollTabsBy(double delta)
    {
        if (TabsScrollViewer == null) return;
        var maxOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
        var newX = Math.Clamp(TabsScrollViewer.Offset.X + delta, 0, maxOffset);
        TabsScrollViewer.Offset = new Vector(newX, TabsScrollViewer.Offset.Y);
        UpdateTabOverflowIndicators();
    }

    private void ScrollSelectedTabIntoView()
    {
        if (TabsScrollViewer == null || TabsItemsControl == null || DataContext is not MainViewModel vm || vm.SelectedTab == null)
            return;

        Dispatcher.UIThread.Post(() =>
        {
            var containers = TabsItemsControl.GetRealizedContainers();
            foreach (var container in containers)
            {
                if (container.DataContext == vm.SelectedTab)
                {
                    var transform = container.TransformToVisual(TabsScrollViewer);
                    if (transform.HasValue)
                    {
                        var rect = new Rect(0, 0, container.Bounds.Width, container.Bounds.Height);
                        var boundsInViewer = rect.TransformToAABB(transform.Value);

                        if (boundsInViewer.Left < 0)
                        {
                            var newX = Math.Max(0, TabsScrollViewer.Offset.X + boundsInViewer.Left - 10);
                            TabsScrollViewer.Offset = new Vector(newX, TabsScrollViewer.Offset.Y);
                        }
                        else if (boundsInViewer.Right > TabsScrollViewer.Viewport.Width)
                        {
                            var maxOffset = Math.Max(0, TabsScrollViewer.Extent.Width - TabsScrollViewer.Viewport.Width);
                            var delta = boundsInViewer.Right - TabsScrollViewer.Viewport.Width + 10;
                            var newX = Math.Min(maxOffset, TabsScrollViewer.Offset.X + delta);
                            TabsScrollViewer.Offset = new Vector(newX, TabsScrollViewer.Offset.Y);
                        }
                    }
                    break;
                }
            }
            UpdateTabOverflowIndicators();
        }, DispatcherPriority.Loaded);
    }

    private void OnTabsPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var visual = e.Source as Visual;
        if (IsCloseButtonClicked(visual)) return;

        var tabVm = FindTabViewModel(visual);
        if (tabVm != null)
        {
            _draggedTab = tabVm;
            _dragStartPos = e.GetPosition(this);
            _isDragging = false;
        }
    }

    private void OnTabsPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTab == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _draggedTab = null;
            _isDragging = false;
            return;
        }

        var currentPos = e.GetPosition(this);
        var delta = currentPos - _dragStartPos;

        if (!_isDragging && (Math.Abs(delta.X) > 6 || Math.Abs(delta.Y) > 6))
        {
            _isDragging = true;
        }

        if (_isDragging && DataContext is MainViewModel mainVm)
        {
            var hitVisual = this.InputHitTest(currentPos) as Visual;
            var hoverTab = FindTabViewModel(hitVisual);

            if (hoverTab != null && hoverTab != _draggedTab)
            {
                mainVm.MoveTab(_draggedTab, hoverTab);
            }
        }
    }

    private void OnTabsPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _draggedTab = null;
        _isDragging = false;
    }

    private void OnTabsPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        _draggedTab = null;
        _isDragging = false;
    }

    private void OnTabsPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainViewModel vm || vm.Tabs.Count <= 1) return;

        // Extract wheel movement (Delta.Y for vertical wheel, Delta.X for horizontal wheel/tilt)
        var delta = e.Delta.Y != 0 ? e.Delta.Y : -e.Delta.X;
        if (Math.Abs(delta) < 0.001) return;

        // Reset accumulator if scrolling direction changed
        if ((_tabWheelAccumulator > 0 && delta < 0) || (_tabWheelAccumulator < 0 && delta > 0))
        {
            _tabWheelAccumulator = 0;
        }

        _tabWheelAccumulator += delta;

        // Process discrete notches: each step consumes ~1.0 notch
        while (_tabWheelAccumulator >= 0.90)
        {
            vm.SelectPreviousTab();
            _tabWheelAccumulator -= 1.0;
            if (_tabWheelAccumulator < 0) _tabWheelAccumulator = 0;
        }

        while (_tabWheelAccumulator <= -0.90)
        {
            vm.SelectNextTab();
            _tabWheelAccumulator += 1.0;
            if (_tabWheelAccumulator > 0) _tabWheelAccumulator = 0;
        }

        e.Handled = true;
    }

    private static TerminalTabViewModel? FindTabViewModel(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button btn && btn.Classes.Contains("tabBtn") && btn.DataContext is TerminalTabViewModel vm)
            {
                return vm;
            }
            visual = visual.GetVisualParent();
        }
        return null;
    }

    private static bool IsCloseButtonClicked(Visual? visual)
    {
        while (visual != null)
        {
            if (visual is Button btn && btn.Content is string s && s == "✕")
            {
                return true;
            }
            if (visual is Button btnTab && btnTab.Classes.Contains("tabBtn"))
            {
                return false;
            }
            visual = visual.GetVisualParent();
        }
        return false;
    }
}