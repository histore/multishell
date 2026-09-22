using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.VisualTree;
using MultiShell.ViewModels;

namespace MultiShell.Views;

/// <summary>
/// Helper to resolve the active terminal control or view for keyboard and overlay focus restoration.
/// Prevents focusing hidden background tabs when multiple tabs are open in the tab panel.
/// </summary>
public static class TerminalFocusHelper
{
    /// <summary>
    /// Finds the effectively visible <see cref="SvcSystems.UI.Terminal.TerminalControl"/> corresponding
    /// to the currently selected tab in the visual tree.
    /// Falls back to any effectively visible terminal control if no direct match is found.
    /// </summary>
    public static SvcSystems.UI.Terminal.TerminalControl? FindActiveTerminal(
        Visual? container,
        TerminalTabViewModel? selectedTab)
    {
        if (container == null) return null;

        var tabViews = container.GetVisualDescendants().OfType<TerminalTabView>().ToList();

        return ResolveActiveControl(
            tabViews,
            selectedTab,
            v => v.DataContext,
            v => v.FindDescendantOfType<SvcSystems.UI.Terminal.TerminalControl>(),
            c => c.IsEffectivelyVisible
        );
    }

    /// <summary>
    /// Core resolution logic: prioritizes the control associated with the selected tab if visible,
    /// otherwise falling back to the first effectively visible control among the views.
    /// </summary>
    public static TControl? ResolveActiveControl<TView, TControl, TTab>(
        IEnumerable<TView> views,
        TTab? selectedTab,
        Func<TView, object?> dataContextSelector,
        Func<TView, TControl?> controlSelector,
        Func<TControl, bool> isVisiblePredicate)
        where TControl : class
    {
        var viewList = views as IList<TView> ?? views.ToList();

        if (selectedTab != null)
        {
            var activeView = viewList.FirstOrDefault(v => ReferenceEquals(dataContextSelector(v), selectedTab));
            if (activeView != null)
            {
                var control = controlSelector(activeView);
                if (control != null && isVisiblePredicate(control))
                {
                    return control;
                }
            }
        }

        return viewList.Select(controlSelector)
            .OfType<TControl>()
            .FirstOrDefault(isVisiblePredicate);
    }
}
