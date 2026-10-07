using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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

        WireClearFilterButton(ClearCommandFilterBtn, tab => tab.CommandFilterQuery = string.Empty);
        WireClearFilterButton(ClearDirectoryFilterBtn, tab => tab.DirectoryFilterQuery = string.Empty);
        WireClearFilterButton(ClearGlobalFilterBtn, tab => tab.GlobalFilterQuery = string.Empty);

        WireHistoryListBox(CommandHistoryListBox);
        WireHistoryListBox(DirectoryHistoryListBox);
        WireHistoryListBox(GlobalHistoryListBox);

        if (HistoryTabControl != null)
        {
            HistoryTabControl.SelectionChanged += (sender, e) =>
            {
                if (e.Source != HistoryTabControl) return;

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
            };
        }

        WireFilterSelectionSync(CommandHistorySearchBox, CommandHistoryListBox);
        WireFilterSelectionSync(DirectoryHistorySearchBox, DirectoryHistoryListBox);
        WireFilterSelectionSync(GlobalHistorySearchBox, GlobalHistoryListBox);
    }

    private void WireClearFilterButton(Button? button, Action<TerminalTabViewModel> clearAction)
    {
        if (button == null) return;
        button.Click += (_, _) =>
        {
            if (DataContext is MainViewModel vm && vm.SelectedTab != null)
            {
                clearAction(vm.SelectedTab);
            }
        };
    }

    private void WireHistoryListBox(ListBox? listBox)
    {
        if (listBox == null) return;
        listBox.KeyDown += OnHistoryListBoxKeyDown;
        listBox.AddHandler(InputElement.PointerPressedEvent, OnHistoryListBoxPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    private static void WireFilterSelectionSync(TextBox? searchBox, ListBox? listBox)
    {
        if (searchBox is null || listBox is null) return;

        searchBox.PropertyChanged += (_, e) =>
        {
            if (e.Property != TextBox.TextProperty) return;

            var filterText = searchBox.Text;
            Dispatcher.UIThread.Post(() =>
            {
                if (listBox.ItemCount <= 0) return;

                listBox.SelectedIndex = !string.IsNullOrWhiteSpace(filterText)
                    ? 0
                    : listBox.ItemCount - 1;

                if (listBox.SelectedItem != null)
                {
                    listBox.ScrollIntoView(listBox.SelectedItem);
                }
            }, DispatcherPriority.Input);
        };
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
                FocusActiveHistoryList();
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
        FocusActiveHistoryList();
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

    private void FocusActiveHistoryList(bool selectFirstItem = true, bool selectLastItem = false)
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
                if (selectLastItem)
                {
                    activeListBox.SelectedIndex = activeListBox.ItemCount - 1;
                }
                else if (selectFirstItem)
                {
                    activeListBox.SelectedIndex = 0;
                }

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
}
