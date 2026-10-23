using System.Linq;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class MainViewModelTabTests
{
    [Fact]
    public async Task TabCycling_NextAndPreviousWithWrapAround_WorksProperly()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Create 3 tabs
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.SelectedTab = mainVm.Tabs[0];

        // Act & Assert 1: Next tab
        mainVm.CycleNextTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);

        mainVm.CycleNextTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Wrap around to first
        mainVm.CycleNextTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Cycle previous wraps around to last
        mainVm.CyclePreviousTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        mainVm.CyclePreviousTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);
    }

    [Fact]
    public async Task SelectTabByIndex_DirectJumpsAndLastTab_WorksProperly()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Create 5 tabs
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(5, mainVm.Tabs.Count);

        // Act & Assert: Select index 2 (3rd tab)
        mainVm.SelectTabByIndex(2);
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Select index 0 (1st tab)
        mainVm.SelectTabByIndex(0);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Select index -1 (last tab)
        mainVm.SelectTabByIndex(-1);
        Assert.Same(mainVm.Tabs[4], mainVm.SelectedTab);

        // Out of bounds does nothing
        mainVm.SelectTabByIndex(99);
        Assert.Same(mainVm.Tabs[4], mainVm.SelectedTab);
    }

    [Fact]
    public async Task MoveSelectedTab_ReordersTabsCorrectly()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);

        var tab0 = mainVm.Tabs[0];
        var tab1 = mainVm.Tabs[1];
        var tab2 = mainVm.Tabs[2];

        mainVm.SelectedTab = tab1;

        // Move right (+1)
        mainVm.MoveSelectedTab(1);
        Assert.Equal(3, mainVm.Tabs.Count);
        Assert.Same(tab0, mainVm.Tabs[0]);
        Assert.Same(tab2, mainVm.Tabs[1]);
        Assert.Same(tab1, mainVm.Tabs[2]);
        Assert.Same(tab1, mainVm.SelectedTab);

        // Move right again (already at end) -> no change
        mainVm.MoveSelectedTab(1);
        Assert.Same(tab1, mainVm.Tabs[2]);

        // Move left twice (-1, -1) -> moves to index 0
        mainVm.MoveSelectedTab(-1);
        Assert.Same(tab1, mainVm.Tabs[1]);
        mainVm.MoveSelectedTab(-1);
        Assert.Same(tab1, mainVm.Tabs[0]);
    }

    [Fact]
    public async Task CloseSelectedTab_ClosesActiveTabAndSelectsAdjacent()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        Assert.Equal(3, mainVm.Tabs.Count);

        var tab1 = mainVm.Tabs[1];
        mainVm.SelectedTab = tab1;

        // Act
        mainVm.CloseSelectedTab();

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.DoesNotContain(tab1, mainVm.Tabs);
        Assert.NotNull(mainVm.SelectedTab);
    }

    [Fact]
    public async Task CloseSelectedTabCommand_ClosesActiveTab_WhenExecuted()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        Assert.Equal(2, mainVm.Tabs.Count);
        var tab1 = mainVm.Tabs[1];
        mainVm.SelectedTab = tab1;

        // Act - Invoked by Window KeyBinding (Ctrl+Shift+W / Ctrl+F4)
        mainVm.CloseSelectedTabCommand.Execute(null);

        // Assert
        Assert.Single(mainVm.Tabs);
        Assert.DoesNotContain(tab1, mainVm.Tabs);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);
    }

    [Fact]
    public async Task ShowTabSwitcher_OpensAndHighlightsNextTab_WhenTriggeredByKeyboard()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.SelectedTab = mainVm.Tabs[0];

        // Act
        mainVm.ShowTabSwitcher(forward: true, isKeyboardTriggered: true);

        // Assert
        Assert.True(mainVm.IsTabSwitcherOpen);
        Assert.True(mainVm.TabSwitcherIsKeyboardTriggered);
        Assert.Equal(1, mainVm.TabSwitcherSelectedIndex);
    }

    [Fact]
    public async Task AdvanceTabSwitcher_ForwardAndBackward_WrapsAround()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.SelectedTab = mainVm.Tabs[0];

        // Act & Assert
        mainVm.ShowTabSwitcher(forward: true, isKeyboardTriggered: true);
        Assert.Equal(1, mainVm.TabSwitcherSelectedIndex);

        mainVm.AdvanceTabSwitcher(forward: true);
        Assert.Equal(2, mainVm.TabSwitcherSelectedIndex);

        mainVm.AdvanceTabSwitcher(forward: true); // Wraps to 0
        Assert.Equal(0, mainVm.TabSwitcherSelectedIndex);

        mainVm.AdvanceTabSwitcher(forward: false); // Wraps back to 2
        Assert.Equal(2, mainVm.TabSwitcherSelectedIndex);
    }

    [Fact]
    public async Task CommitTabSwitcher_AppliesSelectionAndCloses()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.SelectedTab = mainVm.Tabs[0];

        mainVm.ShowTabSwitcher(forward: true, isKeyboardTriggered: true);
        mainVm.AdvanceTabSwitcher(forward: true); // index 2

        // Act
        mainVm.CommitTabSwitcher();

        // Assert
        Assert.False(mainVm.IsTabSwitcherOpen);
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);
    }

    [Fact]
    public async Task CancelTabSwitcher_RevertsToOriginalTabAndCloses()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();
        mainVm.SelectedTab = mainVm.Tabs[0];

        mainVm.ShowTabSwitcher(forward: true, isKeyboardTriggered: true);
        mainVm.AdvanceTabSwitcher(forward: true);

        // Act
        mainVm.CancelTabSwitcher();

        // Assert
        Assert.False(mainVm.IsTabSwitcherOpen);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);
    }

    [Fact]
    public async Task ToggleTabSwitcher_OpensAndCloses()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Act 1: Open
        mainVm.ToggleTabSwitcher();
        Assert.True(mainVm.IsTabSwitcherOpen);
        Assert.False(mainVm.TabSwitcherIsKeyboardTriggered);

        // Act 2: Close
        mainVm.ToggleTabSwitcher();
        Assert.False(mainVm.IsTabSwitcherOpen);
    }

    [Fact]
    public async Task SelectTabFromSwitcher_ActivatesClickedTabAndCloses()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTab();
        mainVm.AddNewTab();

        mainVm.ShowTabSwitcher(forward: false, isKeyboardTriggered: false);

        // Act
        mainVm.SelectTabFromSwitcher(mainVm.Tabs[1]);

        // Assert
        Assert.False(mainVm.IsTabSwitcherOpen);
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);
    }

    [Fact]
    public async Task HasNoTabs_And_HasOpenTabs_ReflectTabStateTransitions()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Initially with 1 restored tab
        Assert.Single(mainVm.Tabs);
        Assert.True(mainVm.HasOpenTabs);
        Assert.False(mainVm.HasNoTabs);

        // Close the only tab -> Empty state
        mainVm.CloseTab(mainVm.Tabs[0]);
        Assert.Empty(mainVm.Tabs);
        Assert.False(mainVm.HasOpenTabs);
        Assert.True(mainVm.HasNoTabs);

        // Add a new tab -> Active state
        mainVm.AddNewTab();
        Assert.Single(mainVm.Tabs);
        Assert.True(mainVm.HasOpenTabs);
        Assert.False(mainVm.HasNoTabs);
    }

    [Fact]
    public async Task AddNewTabWithProfile_FromEmptyState_RestoresActiveState()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        // Close tab to enter empty state
        mainVm.CloseTab(mainVm.Tabs[0]);
        Assert.True(mainVm.HasNoTabs);

        // Act: Open profile from empty state
        var cmdProfile = mainVm.Profiles.FirstOrDefault(p => p.ShellType == ShellType.CMD) ?? mainVm.Profiles.First();
        mainVm.AddNewTabWithProfile(cmdProfile);

        // Assert
        Assert.Single(mainVm.Tabs);
        Assert.True(mainVm.HasOpenTabs);
        Assert.False(mainVm.HasNoTabs);
        Assert.NotNull(mainVm.SelectedTab);
    }

    [Fact]
    public async Task CloseTabFromSwitcher_WhenMultipleTabs_ClosesTargetTabAndKeepsSwitcherOpen()
    {
        // Arrange (REQ-TAB-023)
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabCommand.Execute(null);
        mainVm.AddNewTabCommand.Execute(null);
        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.ShowTabSwitcher(isKeyboardTriggered: false);
        Assert.True(mainVm.IsTabSwitcherOpen);

        var tabToClose = mainVm.Tabs[1];

        // Act
        mainVm.CloseTabFromSwitcherCommand.Execute(tabToClose);

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.DoesNotContain(tabToClose, mainVm.Tabs);
        Assert.True(mainVm.IsTabSwitcherOpen); // Switcher remains open!
        Assert.NotEmpty(mainVm.ClosedTabs); // Tab saved in recently closed history
        Assert.Equal(tabToClose.Title, mainVm.ClosedTabs[0].Title);
    }

    [Fact]
    public async Task CloseTabFromSwitcher_WhenLastTabClosed_ClosesTabSwitcher()
    {
        // Arrange (REQ-TAB-023)
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();
        Assert.Single(mainVm.Tabs);

        mainVm.ShowTabSwitcher(isKeyboardTriggered: false);
        Assert.True(mainVm.IsTabSwitcherOpen);

        var lastTab = mainVm.Tabs[0];

        // Act
        mainVm.CloseTabFromSwitcherCommand.Execute(lastTab);

        // Assert
        Assert.Empty(mainVm.Tabs);
        Assert.False(mainVm.IsTabSwitcherOpen); // Closes when no tabs remain
    }

    [Fact]
    public async Task CloseTabFromSwitcher_WhenSelectedTabIsLastIndex_AdjustsSelectedIndex()
    {
        // Arrange (REQ-TAB-023)
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabCommand.Execute(null);
        mainVm.AddNewTabCommand.Execute(null);

        mainVm.ShowTabSwitcher(isKeyboardTriggered: false);
        mainVm.TabSwitcherSelectedIndex = 2;

        var lastTab = mainVm.Tabs[2];

        // Act
        mainVm.CloseTabFromSwitcherCommand.Execute(lastTab);

        // Assert
        Assert.Equal(2, mainVm.Tabs.Count);
        Assert.Equal(1, mainVm.TabSwitcherSelectedIndex); // Clamped within bounds
        Assert.True(mainVm.IsTabSwitcherOpen);
    }

    [Fact]
    public async Task TabSwitcher_ExposesTabStatusAndVisualParityProperties_ForOverlayBinding()
    {
        // Arrange (REQ-TAB-027)
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        await mainVm.InitializeWorkspaceAsync();

        mainVm.AddNewTabCommand.Execute(null);
        Assert.Equal(2, mainVm.Tabs.Count);

        var runningTab = mainVm.Tabs[0];
        var failedTab = mainVm.Tabs[1];

        // Simulate execution states matching tab header representation
        runningTab.ExecutionState = ProcessExecutionState.Running;
        runningTab.IsOutputActive = true;

        failedTab.ExecutionState = ProcessExecutionState.Failed;
        failedTab.LastExitCode = 127;
        failedTab.SetTabColor("#F7768E");

        // Act
        mainVm.ShowTabSwitcher(isKeyboardTriggered: false);

        // Assert
        Assert.True(mainVm.IsTabSwitcherOpen);
        Assert.Equal(2, mainVm.Tabs.Count);

        // Tab 0: Running & Active Output
        var tab0 = mainVm.Tabs[0];
        Assert.True(tab0.HasProcessStatus);
        Assert.Equal("●", tab0.ExecutionStatusGlyph);
        Assert.False(tab0.HasProcessError);
        Assert.NotEmpty(tab0.ShellIconTag);
        Assert.NotNull(tab0.ExecutionStatusForeground);
        Assert.NotEmpty(tab0.ExecutionStatusTooltip);

        // Tab 1: Failed with non-zero exit code and custom accent color
        var tab1 = mainVm.Tabs[1];
        Assert.True(tab1.HasProcessStatus);
        Assert.True(tab1.HasProcessError);
        Assert.Equal("✕", tab1.ExecutionStatusGlyph);
        Assert.Equal(127, tab1.LastExitCode);
        Assert.True(tab1.HasTabColor);
        Assert.NotNull(tab1.TabColorBrush);
        Assert.NotNull(tab1.ExecutionStatusForeground);
    }
}

