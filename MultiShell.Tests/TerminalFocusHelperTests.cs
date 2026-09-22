using System;
using System.Collections.Generic;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

public class TerminalFocusHelperTests
{
    private class DummyView
    {
        public object? DataContext { get; set; }
        public DummyControl? Control { get; set; }
    }

    private class DummyControl
    {
        public string Name { get; set; } = string.Empty;
        public bool IsEffectivelyVisible { get; set; }
    }

    [Fact]
    public void ResolveActiveControl_WithMultipleTabs_ReturnsSelectedTabControl_NotInactiveTabZero()
    {
        // Arrange: Tab 0 is inactive and hidden, Tab 1 is selected and visible
        var tab0 = new object();
        var tab1 = new object();

        var control0 = new DummyControl { Name = "Terminal_Tab0", IsEffectivelyVisible = false };
        var control1 = new DummyControl { Name = "Terminal_Tab1", IsEffectivelyVisible = true };

        var view0 = new DummyView { DataContext = tab0, Control = control0 };
        var view1 = new DummyView { DataContext = tab1, Control = control1 };

        var views = new List<DummyView> { view0, view1 };

        // Act: Resolve active control for tab1
        var resolved = TerminalFocusHelper.ResolveActiveControl(
            views,
            selectedTab: tab1,
            dataContextSelector: v => v.DataContext,
            controlSelector: v => v.Control,
            isVisiblePredicate: c => c.IsEffectivelyVisible
        );

        // Assert: Must return Tab 1's control, NEVER Tab 0's control
        Assert.NotNull(resolved);
        Assert.Equal("Terminal_Tab1", resolved.Name);
        Assert.True(resolved.IsEffectivelyVisible);
    }

    [Fact]
    public void ResolveActiveControl_WithSingleTab_ReturnsTabZeroControl()
    {
        // Arrange: Only Tab 0 exists and is active/visible
        var tab0 = new object();
        var control0 = new DummyControl { Name = "Terminal_Tab0", IsEffectivelyVisible = true };
        var view0 = new DummyView { DataContext = tab0, Control = control0 };

        var views = new List<DummyView> { view0 };

        // Act
        var resolved = TerminalFocusHelper.ResolveActiveControl(
            views,
            selectedTab: tab0,
            dataContextSelector: v => v.DataContext,
            controlSelector: v => v.Control,
            isVisiblePredicate: c => c.IsEffectivelyVisible
        );

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal("Terminal_Tab0", resolved.Name);
    }

    [Fact]
    public void ResolveActiveControl_WhenSelectedTabNotMatched_FallsBackToVisibleControl()
    {
        // Arrange
        var tab0 = new object();
        var tab1 = new object();
        var unknownTab = new object();

        var control0 = new DummyControl { Name = "Terminal_Tab0", IsEffectivelyVisible = false };
        var control1 = new DummyControl { Name = "Terminal_Tab1", IsEffectivelyVisible = true };

        var view0 = new DummyView { DataContext = tab0, Control = control0 };
        var view1 = new DummyView { DataContext = tab1, Control = control1 };

        var views = new List<DummyView> { view0, view1 };

        // Act
        var resolved = TerminalFocusHelper.ResolveActiveControl(
            views,
            selectedTab: unknownTab,
            dataContextSelector: v => v.DataContext,
            controlSelector: v => v.Control,
            isVisiblePredicate: c => c.IsEffectivelyVisible
        );

        // Assert
        Assert.NotNull(resolved);
        Assert.Equal("Terminal_Tab1", resolved.Name);
    }
}
