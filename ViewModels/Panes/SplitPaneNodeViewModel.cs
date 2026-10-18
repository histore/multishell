using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using MultiShell.Models;

namespace MultiShell.ViewModels;

/// <summary>
/// Intermediate split node in the binary layout tree, dividing space between two child nodes (REQ-UI-006).
/// </summary>
public partial class SplitPaneNodeViewModel : ViewModelBase, IPaneNodeViewModel
{
    public bool IsLeaf => false;

    [ObservableProperty]
    private PaneSplitDirection _orientation;

    [ObservableProperty]
    private double _splitRatio;

    [ObservableProperty]
    private IPaneNodeViewModel _firstChild;

    [ObservableProperty]
    private IPaneNodeViewModel _secondChild;

    public SplitPaneNodeViewModel(
        PaneSplitDirection orientation,
        IPaneNodeViewModel firstChild,
        IPaneNodeViewModel secondChild,
        double splitRatio = 0.5)
    {
        _orientation = orientation;
        _firstChild = firstChild ?? throw new ArgumentNullException(nameof(firstChild));
        _secondChild = secondChild ?? throw new ArgumentNullException(nameof(secondChild));
        _splitRatio = Math.Clamp(splitRatio, 0.05, 0.95);
    }

    public IEnumerable<TerminalPaneViewModel> GetLeaves()
    {
        foreach (var leaf in FirstChild.GetLeaves()) yield return leaf;
        foreach (var leaf in SecondChild.GetLeaves()) yield return leaf;
    }

    public SplitPaneNodeViewModel? FindParent(IPaneNodeViewModel target)
    {
        if (FirstChild == target || SecondChild == target) return this;
        return FirstChild.FindParent(target) ?? SecondChild.FindParent(target);
    }

    public LeafPaneNodeViewModel? FindLeaf(TerminalPaneViewModel pane) =>
        FirstChild.FindLeaf(pane) ?? SecondChild.FindLeaf(pane);
}
