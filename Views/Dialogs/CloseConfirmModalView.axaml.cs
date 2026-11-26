using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MultiShell.ViewModels;

namespace MultiShell.Views.Dialogs;

public partial class CloseConfirmModalView : UserControl
{
    public CloseConfirmModalView()
    {
        InitializeComponent();

        if (CloseConfirmModalOverlay != null)
        {
            CloseConfirmModalOverlay.PointerPressed += (_, e) =>
            {
                if (e.Source == CloseConfirmModalOverlay && DataContext is MainViewModel vm)
                {
                    vm.CancelCloseConfirmation();
                }
            };
        }

        if (ActiveProcessTabsList != null)
        {
            ActiveProcessTabsList.DoubleTapped += (_, _) =>
            {
                if (DataContext is MainViewModel vm && ActiveProcessTabsList.SelectedItem is ActiveProcessTabItem item)
                {
                    vm.SwitchToActiveProcessTab(item);
                }
            };
        }

        PropertyChanged += (_, args) =>
        {
            if (args.Property == IsVisibleProperty && IsVisible)
            {
                Dispatcher.UIThread.Post(() => CancelExitButton?.Focus(), DispatcherPriority.Input);
            }
        };
    }
}
