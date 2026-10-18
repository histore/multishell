namespace MultiShell.Models;

/// <summary>
/// Specifies the orientation when splitting a terminal pane (REQ-UI-006).
/// </summary>
public enum PaneSplitDirection
{
    /// <summary>
    /// Vertical split placing panes side-by-side (left and right columns).
    /// </summary>
    Vertical,

    /// <summary>
    /// Horizontal split placing panes stacked (top and bottom rows).
    /// </summary>
    Horizontal
}
