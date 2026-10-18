using System;
using System.Collections.Generic;
using System.Linq;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class SplitPaneTreeTests
{
    private class TestSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; set; } = "TestSession";
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

#pragma warning disable CS0067
        public event Action<ReadOnlyMemory<byte>>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;
#pragma warning restore CS0067

        public byte[]? LastSentBytes { get; private set; }
        public int LastCols { get; private set; }
        public int LastRows { get; private set; }

        public void Start() => IsRunning = true;
        public void Send(byte[] input) => LastSentBytes = input;
        public void Resize(int cols, int rows)
        {
            LastCols = cols;
            LastRows = rows;
        }
        public void Dispose() => IsRunning = false;
        public void SimulateExit(int code = 0) => Exited?.Invoke(code);
        public void SimulateDataReceived(ReadOnlyMemory<byte> data) => DataReceived?.Invoke(data);
    }

    [Fact]
    public void LeafPaneNode_PropertiesAndMethods_WorkCorrectly()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);
        var pane = new TerminalPaneViewModel(tab, session);
        var leaf = new LeafPaneNodeViewModel(pane);

        Assert.True(leaf.IsLeaf);
        Assert.Same(pane, leaf.Pane);
        Assert.Single(leaf.GetLeaves());
        Assert.Same(pane, leaf.GetLeaves().First());
        Assert.Same(leaf, leaf.FindLeaf(pane));
        Assert.Null(leaf.FindParent(leaf));
    }

    [Fact]
    public void SplitPaneNode_TreeNavigationAndLeaves_WorkCorrectly()
    {
        var session1 = new TestSession { Title = "Pane 1" };
        var session2 = new TestSession { Title = "Pane 2" };
        using var tab = new TerminalTabViewModel(session1);
        var pane1 = new TerminalPaneViewModel(tab, session1);
        var pane2 = new TerminalPaneViewModel(tab, session2);

        var leaf1 = new LeafPaneNodeViewModel(pane1);
        var leaf2 = new LeafPaneNodeViewModel(pane2);
        var split = new SplitPaneNodeViewModel(PaneSplitDirection.Vertical, leaf1, leaf2, 0.5);

        Assert.False(split.IsLeaf);
        Assert.Equal(PaneSplitDirection.Vertical, split.Orientation);
        Assert.Equal(0.5, split.SplitRatio);

        var leaves = split.GetLeaves().ToList();
        Assert.Equal(2, leaves.Count);
        Assert.Same(pane1, leaves[0]);
        Assert.Same(pane2, leaves[1]);

        Assert.Same(split, split.FindParent(leaf1));
        Assert.Same(split, split.FindParent(leaf2));
        Assert.Same(leaf1, split.FindLeaf(pane1));
        Assert.Same(leaf2, split.FindLeaf(pane2));
    }

    [Fact]
    public void SplitPaneNode_ClampsSplitRatioProperly()
    {
        var session1 = new TestSession();
        var session2 = new TestSession();
        using var tab = new TerminalTabViewModel(session1);
        var leaf1 = new LeafPaneNodeViewModel(new TerminalPaneViewModel(tab, session1));
        var leaf2 = new LeafPaneNodeViewModel(new TerminalPaneViewModel(tab, session2));

        var split = new SplitPaneNodeViewModel(PaneSplitDirection.Vertical, leaf1, leaf2, -0.2);
        Assert.Equal(0.05, split.SplitRatio);

        split.SplitRatio = 1.5;
        // Manual assignment clamps via logic or UI
        var clampedSplit = new SplitPaneNodeViewModel(PaneSplitDirection.Horizontal, leaf1, leaf2, 0.99);
        Assert.Equal(0.95, clampedSplit.SplitRatio);
    }

    [Fact]
    public void TerminalTab_InitialState_HasSinglePane()
    {
        var session = new TestSession { Title = "Initial" };
        using var tab = new TerminalTabViewModel(session);

        Assert.Single(tab.Panes);
        Assert.False(tab.HasMultiplePanes);
        Assert.Equal(1, tab.PaneCount);
        Assert.NotNull(tab.ActivePane);
        Assert.True(tab.ActivePane.IsActive);
        Assert.True(tab.RootPaneNode.IsLeaf);
    }

    [Fact]
    public void TerminalTab_SplitRight_CreatesVerticalSplitAndUpdatesProperties()
    {
        var session = new TestSession { Title = "Initial", WorkingDirectory = @"C:\projekte" };
        using var tab = new TerminalTabViewModel(session);
        tab.SessionFactory = (dir, shell) => new TestSession { WorkingDirectory = dir, ShellType = shell };

        var newPane = tab.SplitActivePane(PaneSplitDirection.Vertical);

        Assert.Equal(2, tab.PaneCount);
        Assert.True(tab.HasMultiplePanes);
        Assert.Same(newPane, tab.ActivePane);
        Assert.True(newPane.IsActive);
        Assert.False(tab.Panes[0].IsActive);
        Assert.Equal(@"C:\projekte", newPane.WorkingDirectory);

        Assert.False(tab.RootPaneNode.IsLeaf);
        var split = Assert.IsType<SplitPaneNodeViewModel>(tab.RootPaneNode);
        Assert.Equal(PaneSplitDirection.Vertical, split.Orientation);
    }

    [Fact]
    public void TerminalTab_SplitDown_CreatesHorizontalSplit()
    {
        var session = new TestSession { Title = "Initial" };
        using var tab = new TerminalTabViewModel(session);
        tab.SessionFactory = (dir, shell) => new TestSession();

        var newPane = tab.SplitActivePane(PaneSplitDirection.Horizontal);

        Assert.Equal(2, tab.PaneCount);
        var split = Assert.IsType<SplitPaneNodeViewModel>(tab.RootPaneNode);
        Assert.Equal(PaneSplitDirection.Horizontal, split.Orientation);
    }

    [Fact]
    public void TerminalTab_FocusNavigationAndCycling_SwitchesActivePane()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);
        tab.SessionFactory = (dir, shell) => new TestSession();

        var pane1 = tab.ActivePane;
        var pane2 = tab.SplitActivePane(PaneSplitDirection.Vertical);
        var pane3 = tab.SplitActivePane(PaneSplitDirection.Horizontal);

        Assert.Equal(3, tab.PaneCount);
        Assert.Same(pane3, tab.ActivePane);

        tab.FocusNextPane();
        Assert.Same(pane1, tab.ActivePane);
        Assert.True(pane1.IsActive);
        Assert.False(pane3.IsActive);

        tab.FocusPreviousPane();
        Assert.Same(pane3, tab.ActivePane);

        tab.NavigatePane(PaneNavigationDirection.Left);
        Assert.Same(pane2, tab.ActivePane);
    }

    [Fact]
    public void TerminalTab_ToggleZoom_SwitchesDisplayRootNode()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);
        tab.SessionFactory = (dir, shell) => new TestSession();

        tab.SplitRight();
        Assert.False(tab.IsZoomed);
        Assert.False(tab.DisplayRootNode.IsLeaf);

        tab.ToggleZoom();
        Assert.True(tab.IsZoomed);
        Assert.True(tab.DisplayRootNode.IsLeaf);
        var leaf = Assert.IsType<LeafPaneNodeViewModel>(tab.DisplayRootNode);
        Assert.Same(tab.ActivePane, leaf.Pane);

        tab.ToggleZoom();
        Assert.False(tab.IsZoomed);
        Assert.False(tab.DisplayRootNode.IsLeaf);
    }

    [Fact]
    public void TerminalTab_ResizeActivePane_AdjustsSplitRatio()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);
        tab.SessionFactory = (dir, shell) => new TestSession();

        tab.SplitRight();
        var split = Assert.IsType<SplitPaneNodeViewModel>(tab.RootPaneNode);
        Assert.Equal(0.5, split.SplitRatio);

        // Active pane is second child (right side)
        tab.ResizeActivePane(PaneNavigationDirection.Left, delta: 0.1);
        Assert.Equal(0.4, split.SplitRatio, 2);

        tab.ResizeActivePane(PaneNavigationDirection.Right, delta: 0.2);
        Assert.Equal(0.6, split.SplitRatio, 2);
    }

    [Fact]
    public void TerminalTab_ClosePane_CollapsesSiblingIntoRoot()
    {
        var session1 = new TestSession();
        var session2 = new TestSession();
        using var tab = new TerminalTabViewModel(session1);
        tab.SessionFactory = (dir, shell) => session2;

        var pane2 = tab.SplitActivePane(PaneSplitDirection.Vertical);
        Assert.Equal(2, tab.PaneCount);

        tab.ClosePane(pane2);

        Assert.Equal(1, tab.PaneCount);
        Assert.False(tab.HasMultiplePanes);
        Assert.True(tab.RootPaneNode.IsLeaf);
        Assert.Same(tab.Panes[0], tab.ActivePane);
    }

    [Fact]
    public void TerminalTab_ClosingLastRemainingPane_RequestsTabClose()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);

        var closeRequested = false;
        tab.CloseRequested += _ => closeRequested = true;

        tab.CloseActivePane();

        Assert.True(closeRequested);
        Assert.Empty(tab.Panes);
    }

    [Fact]
    public void TerminalTab_PaneSessionExit_AutoClosesPane()
    {
        var session1 = new TestSession();
        var session2 = new TestSession();
        using var tab = new TerminalTabViewModel(session1);
        tab.SessionFactory = (dir, shell) => session2;

        var pane2 = tab.SplitActivePane(PaneSplitDirection.Vertical);
        Assert.Equal(2, tab.PaneCount);

        session2.SimulateExit(0);

        Assert.Equal(1, tab.PaneCount);
        Assert.Same(tab.Panes[0], tab.ActivePane);
    }

    [Fact]
    public void TerminalTab_MultipleSplits_FiresLayoutChangedAndMaintainsHierarchy()
    {
        var session1 = new TestSession();
        using var tab = new TerminalTabViewModel(session1);
        var layoutFiredCount = 0;
        tab.LayoutChanged += () => layoutFiredCount++;

        // 1st split: 1 -> 2 panes
        tab.SplitRight();
        Assert.Equal(1, layoutFiredCount);
        Assert.Equal(2, tab.PaneCount);

        // 2nd split: 2 -> 3 panes (nested in existing split parent)
        tab.SplitDown();
        Assert.Equal(2, layoutFiredCount);
        Assert.Equal(3, tab.PaneCount);

        // Verify that 3 distinct leaves exist in tree
        var leaves = tab.RootPaneNode.GetLeaves().ToList();
        Assert.Equal(3, leaves.Count);

        // 3rd split: 3 -> 4 panes
        tab.SplitRight();
        Assert.Equal(3, layoutFiredCount);
        Assert.Equal(4, tab.PaneCount);
        Assert.Equal(4, tab.RootPaneNode.GetLeaves().Count());

        // Close 1 pane: 4 -> 3 panes
        tab.CloseActivePane();
        Assert.Equal(4, layoutFiredCount);
        Assert.Equal(3, tab.PaneCount);

        // Toggle zoom: fires LayoutChanged
        tab.ToggleZoom();
        Assert.Equal(5, layoutFiredCount);
        Assert.True(tab.IsZoomed);

        tab.ToggleZoom();
        Assert.Equal(6, layoutFiredCount);
        Assert.False(tab.IsZoomed);
    }

    [Fact]
    public void TerminalPaneViewModel_SizeChanged_ResizesSession()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);
        var pane = new TerminalPaneViewModel(tab, session);
        pane.StartSession();

        pane.Resize(60, 25);
        Assert.Equal(60, session.LastCols);
        Assert.Equal(25, session.LastRows);

        pane.Resize(45, 30);
        Assert.Equal(45, session.LastCols);
        Assert.Equal(30, session.LastRows);
    }

    [Fact]
    public void TerminalPaneViewModel_SendInput_TransmitsToSession()
    {
        var session = new TestSession();
        using var tab = new TerminalTabViewModel(session);
        var pane = new TerminalPaneViewModel(tab, session);
        pane.StartSession();

        var payload = "Get-Process\r\n"u8.ToArray();
        pane.SendInput(payload);
        Assert.Equal(payload, session.LastSentBytes);
    }

    [Fact]
    public void SplitPane_DataReceivedOnSecondPane_FeedsItsTerminalModel()
    {
        var session1 = new TestSession();
        var session2 = new TestSession();
        using var tab = new TerminalTabViewModel(session1);
        tab.SessionFactory = (dir, shell) => session2;

        var pane2 = tab.SplitActivePane(PaneSplitDirection.Vertical);

        // Act
        session2.SimulateDataReceived("Second Pane Prompt> "u8.ToArray());

        // Assert
        var line2 = pane2.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.Equal("Second Pane Prompt>", line2);

        // Initial pane should not have received session2's data
        var line1 = tab.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.DoesNotContain("Second Pane", line1);
    }
}
