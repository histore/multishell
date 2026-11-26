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
    private MainViewModel? _subscribedMainVm;

    public MainWindow()
    {
        InitializeComponent();

        if (TabsItemsControl != null)
        {
            TabsItemsControl.AddHandler(InputElement.PointerPressedEvent, OnTabsPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.PointerMovedEvent, OnTabsPointerMoved, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.PointerReleasedEvent, OnTabsPointerReleased, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.PointerCaptureLostEvent, OnTabsPointerCaptureLost, RoutingStrategies.Tunnel, handledEventsToo: true);
            TabsItemsControl.AddHandler(InputElement.KeyDownEvent, OnTabsKeyDown, RoutingStrategies.Tunnel);
            TabsItemsControl.AddHandler(InputElement.LostFocusEvent, OnTabsLostFocus, RoutingStrategies.Bubble | RoutingStrategies.Tunnel);
        }

        Activated += (_, _) =>
        {
            if (DataContext is MainViewModel vm)
            {
                vm.IsWindowInactive = false;
                vm.UpdateCapsLockState();
            }
        };
        Deactivated += (_, _) =>
        {
            if (DataContext is MainViewModel vm) vm.IsWindowInactive = true;
        };

        AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.CapsLock && DataContext is MainViewModel vm)
            {
                vm.UpdateCapsLockState();
            }
        }, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, (_, e) =>
        {
            if (e.Key == Key.CapsLock && DataContext is MainViewModel vm)
            {
                vm.UpdateCapsLockState();
            }
        }, RoutingStrategies.Tunnel);

        if (TabBarContainer != null)
        {
            TabBarContainer.AddHandler(InputElement.PointerWheelChangedEvent, OnTabsPointerWheelChanged, RoutingStrategies.Bubble);
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
        }

        if (TabScrollLeftBtn != null)
        {
            TabScrollLeftBtn.Click += (_, _) => ScrollTabsBy(-160);
            TabScrollLeftBtn.AddHandler(InputElement.DoubleTappedEvent, (_, e) => e.Handled = true);
        }

        if (TabScrollRightBtn != null)
        {
            TabScrollRightBtn.Click += (_, _) => ScrollTabsBy(160);
            TabScrollRightBtn.AddHandler(InputElement.DoubleTappedEvent, (_, e) => e.Handled = true);
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

        if (SettingsMenuBtn != null)
        {
            SettingsMenuBtn.ProfilesRequested += () =>
            {
                if (DataContext is MainViewModel vm)
                {
                    vm.OpenProfilesModal();
                }
            };
            SettingsMenuBtn.HelpRequested += ShowHelpModal;
            SettingsMenuBtn.AboutRequested += ShowAboutModal;
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

        // Ctrl+Shift+Middle-click on terminal content area / workspace to toggle history overlay (REQ-TAB-012)
        if (TerminalContentArea != null)
        {
            TerminalContentArea.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
            {
                var point = e.GetCurrentPoint(TerminalContentArea);
                var isMiddle = point.Properties.IsMiddleButtonPressed || point.Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonPressed;
                var isCtrl = (e.KeyModifiers & KeyModifiers.Control) != 0;
                var isShift = (e.KeyModifiers & KeyModifiers.Shift) != 0;
                var isAlt = (e.KeyModifiers & KeyModifiers.Alt) != 0;

                if (isMiddle && isCtrl && isShift && !isAlt)
                {
                    ToggleHistoryDrawer();
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);
        }

        if (HistoryDrawer != null)
        {
            HistoryDrawer.CloseRequested += FocusActiveTerminal;
        }

        if (TabSwitcherOverlay != null)
        {
            TabSwitcherOverlay.TabSelected += FocusActiveTerminal;
            TabSwitcherOverlay.DismissRequested += FocusActiveTerminal;
        }

        // Window-level Keyboard Filter
        AddHandler(InputElement.KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyUpEvent, OnWindowKeyUp, RoutingStrategies.Tunnel);

        DataContextChanged += (_, _) =>
        {
            DetachMainViewModelListeners();

            if (DataContext is MainViewModel vm)
            {
                _subscribedMainVm = vm;
                vm.FolderPickerHandler = ShowFolderPickerForBreadcrumbAsync;
                AttachTabRenameListeners(vm);
                vm.PropertyChanged += OnMainViewModelPropertyChanged;
                vm.Tabs.CollectionChanged += OnMainViewModelTabsCollectionChanged;
                vm.ExitConfirmedRequested += OnExitConfirmedRequested;
                vm.CloseConfirmationDismissed += FocusActiveTerminal;
                vm.WindowFocusRequested += BringToForeground;
            }
        };

        Closing += (_, e) =>
        {
            if (DataContext is MainViewModel vm)
            {
                if (vm.TryRequestCloseConfirmation())
                {
                    e.Cancel = true;
                    return;
                }

                if (vm.IsPrimary)
                {
                    vm.SaveCurrentStateSynchronously();
                }
                _ = vm.WindowCoordinator.CloseWindowAsync(vm);
            }
        };

        Closed += (_, _) =>
        {
            StopDragScrollTimer();
            DetachMainViewModelListeners();

            if (DataContext is MainViewModel vm)
            {
                vm.WindowCoordinator.UnregisterWindow(vm);
                if (!vm.IsPrimary && vm.WindowCoordinator.PrimaryWindow is MainViewModel primaryVm)
                {
                    primaryVm.TriggerSaveState();
                }
                vm.Dispose();
            }
        };
    }

    private void OnMainViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainViewModel.SelectedTab))
        {
            ScrollSelectedTabIntoView();
            FocusActiveTerminal();
        }
    }

    private void OnMainViewModelTabsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            UpdateTabOverflowIndicators();
            ScrollSelectedTabIntoView();
        }, DispatcherPriority.Loaded);
    }

    private void OnExitConfirmedRequested()
    {
        Dispatcher.UIThread.Post(Close);
    }

    private void DetachMainViewModelListeners()
    {
        if (_subscribedMainVm == null) return;

        _subscribedMainVm.PropertyChanged -= OnMainViewModelPropertyChanged;
        _subscribedMainVm.Tabs.CollectionChanged -= OnMainViewModelTabsCollectionChanged;
        _subscribedMainVm.ExitConfirmedRequested -= OnExitConfirmedRequested;
        _subscribedMainVm.CloseConfirmationDismissed -= FocusActiveTerminal;
        _subscribedMainVm.WindowFocusRequested -= BringToForeground;
        DetachTabRenameListeners(_subscribedMainVm);
        if (_subscribedMainVm.FolderPickerHandler == ShowFolderPickerForBreadcrumbAsync)
        {
            _subscribedMainVm.FolderPickerHandler = null;
        }
        _subscribedMainVm = null;
    }

    public void ShowHelpModal()
    {
        if (HelpModal != null)
        {
            HelpModal.IsVisible = true;
            HelpModal.Focus();
        }
    }

    public void HideHelpModal()
    {
        if (HelpModal != null) HelpModal.IsVisible = false;
        FocusActiveTerminal();
    }

    public void ShowAboutModal()
    {
        if (AboutModal != null)
        {
            AboutModal.IsVisible = true;
            AboutModal.Focus();
        }
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
        if (DataContext is MainViewModel mainVm && mainVm.SelectedTab?.IsRenaming == true)
        {
            return;
        }

        void DoFocus()
        {
            if (DataContext is not MainViewModel vm || vm.SelectedTab?.IsRenaming == true)
            {
                return;
            }

            var targetTerminal = GetActiveTerminalControl();
            if (targetTerminal != null && targetTerminal.IsEffectivelyVisible)
            {
                targetTerminal.Focus();
            }
            else if (vm.SelectedTab != null)
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

        var filter = new List<FilePickerFileType>
        {
            new(vm.Loc["Profiles_Browse_Exe_Filter"])
            {
                Patterns = new[] { "*.exe", "*.cmd", "*.bat", "*.*" }
            },
            FilePickerFileTypes.All
        };

        var initialDir = !string.IsNullOrWhiteSpace(vm.EditingExecutablePath)
            ? System.IO.Path.GetDirectoryName(vm.EditingExecutablePath)
            : null;

        var path = await StoragePickerHelper.PickSingleFileAsync(
            this,
            vm.Loc["Profiles_Browse_Exe_Title"],
            initialDir,
            filter,
            nameof(MainWindow));

        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.EditingExecutablePath = path;
        }
    }

    private async Task BrowseWorkingDirectoryAsync()
    {
        if (DataContext is not MainViewModel vm) return;

        var path = await StoragePickerHelper.PickSingleFolderAsync(
            this,
            vm.Loc["Profiles_Browse_Dir_Title"],
            vm.EditingWorkingDirectory,
            nameof(MainWindow));

        if (!string.IsNullOrWhiteSpace(path))
        {
            vm.EditingWorkingDirectory = path;
        }
    }

    private async Task<string?> ShowFolderPickerForBreadcrumbAsync(string? initialDirectory)
    {
        try
        {
            var title = (DataContext as MainViewModel)?.Loc["Dialog_PickFolder_Title"] ?? "Select Folder";
            return await StoragePickerHelper.PickSingleFolderAsync(this, title, initialDirectory, nameof(MainWindow));
        }
        finally
        {
            FocusActiveTerminal();
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
        if (handle.HasValue)
        {
            MultiShell.Services.WindowsPlatformService.Instance.SetWindowForeground(handle.Value);
        }
    }
}
