using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MultiShell.ViewModels;

/// <summary>
/// Leaf node in the split pane tree wrapping a concrete TerminalPaneViewModel (REQ-UI-006).
/// </summary>
public partial class LeafPaneNodeViewModel : ViewModelBase, IPaneNodeViewModel
{
    public bool IsLeaf => true;

    /// <summary>
    /// Gets the terminal pane wrapped by this leaf node.
    /// </summary>
    public TerminalPaneViewModel Pane { get; }

    public LeafPaneNodeViewModel(TerminalPaneViewModel pane)
    {
        Pane = pane ?? throw new ArgumentNullException(nameof(pane));
    }

    public IEnumerable<TerminalPaneViewModel> GetLeaves()
    {
        yield return Pane;
    }

    public SplitPaneNodeViewModel? FindParent(IPaneNodeViewModel target) => null;

    public LeafPaneNodeViewModel? FindLeaf(TerminalPaneViewModel pane) =>
        Pane == pane ? this : null;
}
