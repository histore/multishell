using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views.Dialogs;

public partial class HistoryDrawerView : UserControl
{
    public event Action? CloseRequested;

    public bool IsDrawerOpen => HistoryDrawerGrid?.IsVisible == true;

    public HistoryDrawerView()
    {
        InitializeComponent();

        if (CloseHistoryButton != null)
        {
            CloseHistoryButton.Click += (_, _) => HideHistoryDrawer();
        }

        if (HistoryDrawerGrid != null)
        {
            HistoryDrawerGrid.PointerPressed += (_, e) =>
            {
                if (e.Source == HistoryDrawerGrid)
                {
                    HideHistoryDrawer();
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
            };
        }

        if (ClearGlobalFilterBtn != null)
        {
            ClearGlobalFilterBtn.Click += (_, _) =>
            {
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    vm.SelectedTab.GlobalFilterQuery = string.Empty;
                }
            };
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

        if (GlobalHistoryListBox != null)
        {
            GlobalHistoryListBox.KeyDown += OnHistoryListBoxKeyDown;
            GlobalHistoryListBox.AddHandler(InputElement.PointerPressedEvent, OnHistoryListBoxPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
        }

        if (HistoryTabControl != null)
        {
            HistoryTabControl.SelectionChanged += (sender, e) =>
            {
                if (e.Source == HistoryTabControl)
                {
                    var hasFilter = false;
                    if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                    {
                        hasFilter = HistoryTabControl.SelectedIndex switch
                        {
                            1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                            2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                            _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                        };
                    }
                    FocusActiveHistoryList(selectLastItem: !hasFilter);
                }
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

        if (GlobalHistorySearchBox != null)
        {
            GlobalHistorySearchBox.PropertyChanged += (_, e) =>
            {
                if (e.Property == TextBox.TextProperty)
                {
                    var filterText = GlobalHistorySearchBox.Text;
                    Dispatcher.UIThread.Post(() =>
                    {
                        if (GlobalHistoryListBox != null && GlobalHistoryListBox.ItemCount > 0)
                        {
                            GlobalHistoryListBox.SelectedIndex = !string.IsNullOrWhiteSpace(filterText)
                                ? 0
                                : GlobalHistoryListBox.ItemCount - 1;

                            if (GlobalHistoryListBox.SelectedItem != null)
                            {
                                GlobalHistoryListBox.ScrollIntoView(GlobalHistoryListBox.SelectedItem);
                            }
                        }
                    }, DispatcherPriority.Input);
                }
            };
        }
    }

    public void ToggleHistoryDrawer()
    {
        if (HistoryDrawerGrid == null) return;

        if (HistoryDrawerGrid.IsVisible)
        {
            HideHistoryDrawer();
        }
        else
        {
            ShowHistoryDrawer();
        }
    }

    public void OpenOrToggleHistoryDrawer(int targetTabIndex)
    {
        if (HistoryDrawerGrid == null) return;

        if (HistoryDrawerGrid.IsVisible)
        {
            if (HistoryTabControl != null && HistoryTabControl.SelectedIndex == targetTabIndex)
            {
                HideHistoryDrawer();
            }
            else
            {
                if (HistoryTabControl != null)
                {
                    HistoryTabControl.SelectedIndex = targetTabIndex;
                }
                var hasFilter = false;
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    hasFilter = targetTabIndex switch
                    {
                        1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                        2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                        _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                    };
                }
                FocusActiveHistoryList(selectLastItem: !hasFilter);
            }
        }
        else
        {
            if (HistoryTabControl != null)
            {
                HistoryTabControl.SelectedIndex = targetTabIndex;
            }
            ShowHistoryDrawer();
        }
    }

    public void ShowHistoryDrawer()
    {
        if (HistoryDrawerGrid == null) return;
        HistoryDrawerGrid.IsVisible = true;
        var hasFilter = false;
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            hasFilter = HistoryTabControl?.SelectedIndex switch
            {
                1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
            };
        }
        FocusActiveHistoryList(selectLastItem: !hasFilter);
    }

    public void HideHistoryDrawer()
    {
        if (HistoryDrawerGrid != null)
        {
            HistoryDrawerGrid.IsVisible = false;
        }
        CloseRequested?.Invoke();
    }

    public void NavigateHistorySelection(int delta)
    {
        var tabIndex = HistoryTabControl?.SelectedIndex ?? 0;
        ListBox? activeListBox = tabIndex switch
        {
            1 => DirectoryHistoryListBox,
            2 => GlobalHistoryListBox,
            _ => CommandHistoryListBox
        };

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

    public bool HandleKeyDown(KeyEventArgs e)
    {
        if (!IsDrawerOpen) return false;

        if (e.Key == Key.Escape)
        {
            if (DataContext is MainViewModel vm && vm.SelectedTab != null)
            {
                if (HistoryTabControl?.SelectedIndex == 2 && !string.IsNullOrEmpty(vm.SelectedTab.GlobalFilterQuery))
                {
                    vm.SelectedTab.GlobalFilterQuery = string.Empty;
                    e.Handled = true;
                    return true;
                }
                if (HistoryTabControl?.SelectedIndex == 1 && !string.IsNullOrEmpty(vm.SelectedTab.DirectoryFilterQuery))
                {
                    vm.SelectedTab.DirectoryFilterQuery = string.Empty;
                    e.Handled = true;
                    return true;
                }
                if (HistoryTabControl?.SelectedIndex == 0 && !string.IsNullOrEmpty(vm.SelectedTab.CommandFilterQuery))
                {
                    vm.SelectedTab.CommandFilterQuery = string.Empty;
                    e.Handled = true;
                    return true;
                }
            }
            HideHistoryDrawer();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Enter)
        {
            var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            if (HistoryTabControl?.SelectedIndex == 2)
            {
                if (isShift) PasteSelectedGlobalItem();
                else ExecuteSelectedGlobalItem();
            }
            else if (HistoryTabControl?.SelectedIndex == 1)
            {
                if (isShift) PasteSelectedDirectory();
                else ExecuteSelectedDirectory();
            }
            else
            {
                if (isShift) PasteSelectedCommand();
                else ExecuteSelectedCommand();
            }
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Up)
        {
            NavigateHistorySelection(-1);
            ClearSearchBoxSelection();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Down)
        {
            NavigateHistorySelection(1);
            ClearSearchBoxSelection();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Left)
        {
            if (e.Source is TextBox) return false;

            if (HistoryTabControl != null)
            {
                var next = (HistoryTabControl.SelectedIndex - 1 + 3) % 3;
                HistoryTabControl.SelectedIndex = next;
                var hasFilter = false;
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    hasFilter = next switch
                    {
                        1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                        2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                        _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                    };
                }
                FocusActiveHistoryList(selectLastItem: !hasFilter);
            }
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Right)
        {
            if (e.Source is TextBox) return false;

            if (HistoryTabControl != null)
            {
                var next = (HistoryTabControl.SelectedIndex + 1) % 3;
                HistoryTabControl.SelectedIndex = next;
                var hasFilter = false;
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    hasFilter = next switch
                    {
                        1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                        2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                        _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                    };
                }
                FocusActiveHistoryList(selectLastItem: !hasFilter);
            }
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Tab)
        {
            if (HistoryTabControl != null)
            {
                var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
                var next = isShift
                    ? (HistoryTabControl.SelectedIndex - 1 + 3) % 3
                    : (HistoryTabControl.SelectedIndex + 1) % 3;
                HistoryTabControl.SelectedIndex = next;
                var hasFilter = false;
                if (DataContext is MainViewModel vm && vm.SelectedTab != null)
                {
                    hasFilter = next switch
                    {
                        1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                        2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                        _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                    };
                }
                FocusActiveHistoryList(selectLastItem: !hasFilter);
            }
            e.Handled = true;
            return true;
        }

        return false;
    }

    private void ClearSearchBoxSelection()
    {
        var tabIndex = HistoryTabControl?.SelectedIndex ?? 0;
        TextBox? activeSearchBox = tabIndex switch
        {
            1 => DirectoryHistorySearchBox,
            2 => GlobalHistorySearchBox,
            _ => CommandHistorySearchBox
        };

        if (activeSearchBox != null && activeSearchBox.SelectionStart != activeSearchBox.SelectionEnd)
        {
            activeSearchBox.CaretIndex = activeSearchBox.Text?.Length ?? 0;
            activeSearchBox.SelectionStart = activeSearchBox.CaretIndex;
            activeSearchBox.SelectionEnd = activeSearchBox.CaretIndex;
        }
    }

    private void FocusActiveHistoryList(bool selectLastItem = true)
    {
        void DoFocusAndSelect()
        {
            var tabIndex = HistoryTabControl?.SelectedIndex ?? 0;
            ListBox? activeListBox = tabIndex switch
            {
                1 => DirectoryHistoryListBox,
                2 => GlobalHistoryListBox,
                _ => CommandHistoryListBox
            };
            TextBox? activeSearchBox = tabIndex switch
            {
                1 => DirectoryHistorySearchBox,
                2 => GlobalHistorySearchBox,
                _ => CommandHistorySearchBox
            };

            if (activeListBox != null && activeListBox.ItemCount > 0)
            {
                activeListBox.SelectedIndex = (tabIndex == 2 || !selectLastItem) ? 0 : activeListBox.ItemCount - 1;

                if (activeListBox.SelectedItem != null)
                {
                    activeListBox.ScrollIntoView(activeListBox.SelectedItem);
                }
            }

            if (activeSearchBox != null)
            {
                activeSearchBox.Focus();
                if (!string.IsNullOrEmpty(activeSearchBox.Text))
                {
                    activeSearchBox.SelectAll();
                }
            }
        }

        Dispatcher.UIThread.Post(DoFocusAndSelect, DispatcherPriority.Input);
        Dispatcher.UIThread.Post(DoFocusAndSelect, DispatcherPriority.Loaded);
    }

    private void OnHistoryListBoxPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(this).Properties;

        if (props.IsLeftButtonPressed)
        {
            e.Handled = true;
            if (sender == CommandHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) PasteSelectedCommand(clickedItem);
            }
            else if (sender == DirectoryHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) PasteSelectedDirectory(clickedItem);
            }
            else if (sender == GlobalHistoryListBox)
            {
                var clickedItem = GetGlobalHistoryItemFromPointerSource(e.Source as Visual);
                if (clickedItem != null) PasteSelectedGlobalItem(clickedItem);
            }
        }
        else if (props.IsRightButtonPressed)
        {
            e.Handled = true;
            if (sender == CommandHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) ExecuteSelectedCommand(clickedItem);
            }
            else if (sender == DirectoryHistoryListBox)
            {
                var clickedItem = GetHistoryItemFromPointerSource(e.Source as Visual);
                if (!string.IsNullOrWhiteSpace(clickedItem)) ExecuteSelectedDirectory(clickedItem);
            }
            else if (sender == GlobalHistoryListBox)
            {
                var clickedItem = GetGlobalHistoryItemFromPointerSource(e.Source as Visual);
                if (clickedItem != null) ExecuteSelectedGlobalItem(clickedItem);
            }
        }
    }

    private static GlobalHistoryItem? GetGlobalHistoryItemFromPointerSource(Visual? visual)
    {
        while (visual != null)
        {
            if (visual.DataContext is GlobalHistoryItem item)
            {
                return item;
            }
            visual = visual.GetVisualParent();
        }
        return null;
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

    private void OnHistoryListBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
            if (sender == GlobalHistoryListBox || HistoryTabControl?.SelectedIndex == 2)
            {
                if (isShift) PasteSelectedGlobalItem();
                else ExecuteSelectedGlobalItem();
            }
            else if (sender == CommandHistoryListBox || HistoryTabControl?.SelectedIndex == 0)
            {
                if (isShift) PasteSelectedCommand();
                else ExecuteSelectedCommand();
            }
            else
            {
                if (isShift) PasteSelectedDirectory();
                else ExecuteSelectedDirectory();
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Left && HistoryTabControl != null)
        {
            var next = (HistoryTabControl.SelectedIndex - 1 + 3) % 3;
            HistoryTabControl.SelectedIndex = next;
            var hasFilter = false;
            if (DataContext is MainViewModel vm && vm.SelectedTab != null)
            {
                hasFilter = next switch
                {
                    1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                    2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                    _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                };
            }
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Right && HistoryTabControl != null)
        {
            var next = (HistoryTabControl.SelectedIndex + 1) % 3;
            HistoryTabControl.SelectedIndex = next;
            var hasFilter = false;
            if (DataContext is MainViewModel vm && vm.SelectedTab != null)
            {
                hasFilter = next switch
                {
                    1 => !string.IsNullOrWhiteSpace(vm.SelectedTab.DirectoryFilterQuery),
                    2 => !string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery),
                    _ => !string.IsNullOrWhiteSpace(vm.SelectedTab.CommandFilterQuery)
                };
            }
            FocusActiveHistoryList(selectLastItem: !hasFilter);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            HideHistoryDrawer();
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
            HideHistoryDrawer();
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
            HideHistoryDrawer();
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
            HideHistoryDrawer();
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
            HideHistoryDrawer();
        }
    }

    private void ExecuteSelectedGlobalItem(GlobalHistoryItem? explicitItem = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var item = explicitItem ?? GlobalHistoryListBox?.SelectedItem as GlobalHistoryItem;
            if (item == null)
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery) && vm.SelectedTab.FilteredGlobalHistory.Count > 0)
                {
                    item = vm.SelectedTab.FilteredGlobalHistory[0];
                }
                else if (vm.SelectedTab.GlobalHistory.Count > 0)
                {
                    item = vm.SelectedTab.GlobalHistory[^1];
                }
            }
            if (item != null)
            {
                vm.SelectedTab.ExecuteGlobalItem(item);
            }
            HideHistoryDrawer();
        }
    }

    private void PasteSelectedGlobalItem(GlobalHistoryItem? explicitItem = null)
    {
        if (DataContext is MainViewModel vm && vm.SelectedTab != null)
        {
            var item = explicitItem ?? GlobalHistoryListBox?.SelectedItem as GlobalHistoryItem;
            if (item == null)
            {
                if (!string.IsNullOrWhiteSpace(vm.SelectedTab.GlobalFilterQuery) && vm.SelectedTab.FilteredGlobalHistory.Count > 0)
                {
                    item = vm.SelectedTab.FilteredGlobalHistory[0];
                }
                else if (vm.SelectedTab.GlobalHistory.Count > 0)
                {
                    item = vm.SelectedTab.GlobalHistory[^1];
                }
            }
            if (item != null)
            {
                vm.SelectedTab.PasteGlobalItem(item);
            }
            HideHistoryDrawer();
        }
    }
}
