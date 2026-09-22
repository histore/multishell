using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class MainWindow : Window
{
    private DispatcherTimer? _historyHoverTimer;

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
            TabBarContainer.AddHandler(InputElement.PointerPressedEvent, OnTabBarPointerPressed, RoutingStrategies.Bubble);
            TabBarContainer.AddHandler(DragDrop.DragEnterEvent, OnTabBarDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            TabBarContainer.AddHandler(DragDrop.DragOverEvent, OnTabBarDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            TabBarContainer.AddHandler(DragDrop.DragLeaveEvent, OnTabBarDragLeave, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            TabBarContainer.AddHandler(DragDrop.DropEvent, OnTabBarDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }

        if (TerminalContentArea != null)
        {
            TerminalContentArea.AddHandler(DragDrop.DragEnterEvent, OnTerminalDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            TerminalContentArea.AddHandler(DragDrop.DragOverEvent, OnTerminalDragOver, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            TerminalContentArea.AddHandler(DragDrop.DropEvent, OnTerminalDrop, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
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
        if (HelpModal != null)
        {
            HelpModal.CloseRequested += (_, _) => HideHelpModal();
        }

        if (AboutModal != null)
        {
            AboutModal.CloseRequested += (_, _) => HideAboutModal();
        }

        if (ProfilesModal != null)
        {
            ProfilesModal.CloseRequested += (_, _) =>
            {
                if (DataContext is MainViewModel vm) vm.CloseProfilesModal();
            };
            ProfilesModal.BrowseExecutableRequested += async (_, _) => await BrowseExecutablePathAsync();
            ProfilesModal.BrowseWorkingDirRequested += async (_, _) => await BrowseWorkingDirectoryAsync();
        }

        // History Drawer Hover Trigger with Dwell Delay (REQ-TAB-012)
        if (HistoryHoverTrigger != null)
        {
            HistoryHoverTrigger.PointerEntered += (_, _) =>
            {
                _historyHoverTimer?.Stop();
                if (HistoryDrawer?.IsVisible == true) return;

                _historyHoverTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(300)
                };
                _historyHoverTimer.Tick += (_, _) =>
                {
                    _historyHoverTimer?.Stop();
                    _historyHoverTimer = null;
                    if (HistoryDrawer != null && !HistoryDrawer.IsVisible)
                    {
                        ShowHistoryDrawer();
                    }
                };
                _historyHoverTimer.Start();
            };

            HistoryHoverTrigger.PointerExited += (_, _) =>
            {
                _historyHoverTimer?.Stop();
                _historyHoverTimer = null;
            };
        }

        if (HistoryDrawer != null)
        {
            HistoryDrawer.PointerExited += (_, e) =>
            {
                var pos = e.GetPosition(HistoryDrawer);
                if (pos.X <= 0 || pos.X >= HistoryDrawer.Bounds.Width - 1 || pos.Y <= 0 || pos.Y >= HistoryDrawer.Bounds.Height - 1)
                {
                    HideHistoryDrawerAndFocusTerminal();
                }
            };
        }

        // Close History Drawer when pointer leaves the main window in any direction
        PointerExited += (_, _) =>
        {
            _historyHoverTimer?.Stop();
            _historyHoverTimer = null;
            if (HistoryDrawer?.IsVisible == true)
            {
                HideHistoryDrawerAndFocusTerminal();
            }
        };

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

    internal SvcSystems.UI.Terminal.TerminalControl? GetActiveTerminalControl()
    {
        var vm = DataContext as MainViewModel;
        return TerminalFocusHelper.FindActiveTerminal(TabContentControl, vm?.SelectedTab);
    }

    private void FocusActiveTerminal()
    {
        void DoFocus()
        {
            var targetTerminal = GetActiveTerminalControl();
            if (targetTerminal != null && targetTerminal.IsEffectivelyVisible)
            {
                targetTerminal.Focus();
            }
            else if (DataContext is MainViewModel vm && vm.SelectedTab != null)
            {
                var activeTabView = TabContentControl?.GetVisualDescendants()
                    .OfType<TerminalTabView>()
                    .FirstOrDefault(v => ReferenceEquals(v.DataContext, vm.SelectedTab));

                if (activeTabView != null && activeTabView.IsEffectivelyVisible)
                {
                    activeTabView.FocusTerminal();
                }
                else
                {
                    TabContentControl?.Focus();
                }
            }
            else
            {
                TabContentControl?.Focus();
            }
        }

        // Apply focus immediately and asynchronously across dispatcher priorities
        // to guarantee focus acquisition after overlay closure and layout invalidation.
        DoFocus();
        Dispatcher.UIThread.Post(DoFocus, DispatcherPriority.Input);
        Dispatcher.UIThread.Post(DoFocus, DispatcherPriority.Loaded);
    }

    private async Task BrowseExecutablePathAsync()
    {
        if (DataContext is not MainViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var options = new FilePickerOpenOptions
        {
            Title = vm.Loc["Profiles_Browse_Exe_Title"],
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Executables (*.exe, *.cmd, *.bat)")
                {
                    Patterns = new[] { "*.exe", "*.cmd", "*.bat", "*.*" }
                },
                FilePickerFileTypes.All
            }
        };

        if (!string.IsNullOrWhiteSpace(vm.EditingExecutablePath))
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(vm.EditingExecutablePath);
                if (!string.IsNullOrEmpty(dir) && System.IO.Directory.Exists(dir))
                {
                    var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(dir);
                    if (folder != null)
                    {
                        options.SuggestedStartLocation = folder;
                    }
                }
            }
            catch { }
        }

        var results = await topLevel.StorageProvider.OpenFilePickerAsync(options);
        if (results.Count > 0)
        {
            var path = results[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                vm.EditingExecutablePath = path;
            }
        }
    }

    private async Task BrowseWorkingDirectoryAsync()
    {
        if (DataContext is not MainViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider == null) return;

        var options = new FolderPickerOpenOptions
        {
            Title = vm.Loc["Profiles_Browse_Dir_Title"],
            AllowMultiple = false
        };

        var initialDir = !string.IsNullOrWhiteSpace(vm.EditingWorkingDirectory)
            ? vm.EditingWorkingDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (!string.IsNullOrWhiteSpace(initialDir) && System.IO.Directory.Exists(initialDir))
        {
            try
            {
                var folder = await topLevel.StorageProvider.TryGetFolderFromPathAsync(initialDir);
                if (folder != null)
                {
                    options.SuggestedStartLocation = folder;
                }
            }
            catch { }
        }

        var results = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
        if (results.Count > 0)
        {
            var path = results[0].TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(path))
            {
                vm.EditingWorkingDirectory = path;
            }
        }
    }

    /// <summary>
    /// Restores, activates, and brings the window into the foreground.
    /// </summary>
    public void BringToForeground()
    {
        if (WindowState == WindowState.Minimized)
        {
            WindowState = WindowState.Normal;
        }

        Show();
        Activate();
        Focus();

        var handle = TryGetPlatformHandle()?.Handle;
        if (handle.HasValue && handle.Value != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            SetForegroundWindow(handle.Value);
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
