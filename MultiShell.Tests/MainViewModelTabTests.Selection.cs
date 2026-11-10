using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class MainViewModelTabTests
{
    [Fact]
    public void SelectTab_UpdatesSelectedTabProperty()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab();

        // Act
        mainVm.SelectTab(mainVm.Tabs[0]);

        // Assert
        Assert.Equal("PS 1", mainVm.SelectedTab?.Title);
    }

    [Fact]
    public void SelectNextTab_AdvancesToNextTab_And_ClampsAtEnd()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab();
        mainVm.AddNewTab();

        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.SelectTab(mainVm.Tabs[0]);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Act & Assert 1: Move from 0 to 1
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);

        // Act & Assert 2: Move from 1 to 2
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Act & Assert 3: Already at last tab, clamp at index 2
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);
    }

    [Fact]
    public void SelectPreviousTab_MovesToPreviousTab_And_ClampsAtStart()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        mainVm.AddNewTab();
        mainVm.AddNewTab();

        Assert.Equal(3, mainVm.Tabs.Count);

        mainVm.SelectTab(mainVm.Tabs[2]);
        Assert.Same(mainVm.Tabs[2], mainVm.SelectedTab);

        // Act & Assert 1: Move from 2 to 1
        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[1], mainVm.SelectedTab);

        // Act & Assert 2: Move from 1 to 0
        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Act & Assert 3: Already at first tab, clamp at index 0
        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);
    }

    [Fact]
    public void SelectNextTab_And_SelectPreviousTab_WhenSingleTab_DoesNothing()
    {
        // Arrange
        var processService = new FakePowerShellProcessService();
        var persistenceService = new FakeTabStatePersistenceService();
        using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
        Assert.Single(mainVm.Tabs);
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        // Act & Assert
        mainVm.SelectNextTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);

        mainVm.SelectPreviousTab();
        Assert.Same(mainVm.Tabs[0], mainVm.SelectedTab);
    }
}
