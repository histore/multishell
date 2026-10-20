using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class TerminalTabView : UserControl
{
    public TerminalTabView()
    {
        InitializeComponent();

        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && e.NewValue is true)
            {
                Dispatcher.UIThread.Post(FocusTerminal, DispatcherPriority.Input);
                Dispatcher.UIThread.Post(FocusTerminal, DispatcherPriority.Loaded);
            }
        };
    }

    /// <summary>
    /// Explicitly focuses the inner active TerminalControl across all split panes.
    /// </summary>
    public void FocusTerminal()
    {
        var activePaneView = this.GetVisualDescendants()
            .OfType<TerminalPaneView>()
            .FirstOrDefault(p => (p.DataContext as TerminalPaneViewModel)?.IsActive == true)
            ?? this.FindDescendantOfType<TerminalPaneView>();

        activePaneView?.FocusTerminal();
    }
}
