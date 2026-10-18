using System.Linq;
using Avalonia.Controls;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

public partial class TerminalTabView : UserControl
{
    public TerminalTabView()
    {
        InitializeComponent();
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
