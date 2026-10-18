using System;
using System.Collections.Generic;

namespace MultiShell.ViewModels;

/// <summary>
/// Represents a node in the binary layout tree of split panes within a tab (REQ-UI-006).
/// </summary>
public interface IPaneNodeViewModel
{
    /// <summary>
    /// Gets whether this node is a leaf containing a single terminal pane.
    /// </summary>
    bool IsLeaf { get; }

    /// <summary>
    /// Enumerates all leaf terminal panes contained within this subtree in visual order.
    /// </summary>
    IEnumerable<TerminalPaneViewModel> GetLeaves();

    /// <summary>
    /// Finds the parent split node of the specified target node within this subtree, or null if target is root or not found.
    /// </summary>
    SplitPaneNodeViewModel? FindParent(IPaneNodeViewModel target);

    /// <summary>
    /// Finds the leaf node wrapping the specified terminal pane within this subtree, or null if not found.
    /// </summary>
    LeafPaneNodeViewModel? FindLeaf(TerminalPaneViewModel pane);
}
