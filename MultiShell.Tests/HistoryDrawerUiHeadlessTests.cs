using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views.Dialogs;
using Xunit;

namespace MultiShell.Tests;

[CollectionDefinition("HeadlessUI", DisableParallelization = true)]
public class HeadlessUiTestCollection { }

[Collection("HeadlessUI")]
public class HistoryDrawerUiHeadlessTests
{
    [Fact]
    public async Task HistoryDrawerView_WhenOpenedWithExistingSearchText_SelectsAllText()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            // Arrange
            var view = new HistoryDrawerView();
            var searchBox = view.FindControl<TextBox>("CommandHistorySearchBox");
            Assert.NotNull(searchBox);

            searchBox.Text = "docker ps";

            // Act - Show drawer which focuses and selects text
            view.ShowHistoryDrawer();

            // Process dispatcher queue (both Input and Loaded priority)
            Dispatcher.UIThread.RunJobs();

            // Assert
            Assert.Equal("docker ps", searchBox.Text);
            Assert.Equal(0, searchBox.SelectionStart);
            Assert.Equal(searchBox.Text!.Length, searchBox.SelectionEnd);
        });
    }

    [Fact]
    public async Task HistoryDrawerView_WhenViewModelRequestsFocusAndSelect_SelectsAllText()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            // Arrange
            var processService = new MockShellProcessService();
            var persistenceService = new MockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var tab = mainVm.SelectedTab;
            Assert.NotNull(tab);
            tab.CommandFilterQuery = "git status";

            var view = new HistoryDrawerView
            {
                DataContext = mainVm
            };
            view.ShowHistoryDrawer();
            Dispatcher.UIThread.RunJobs();

            // Act - trigger from ViewModel intent
            tab.RequestFocusAndSelectHistorySearch();
            Dispatcher.UIThread.RunJobs();

            // Assert
            var searchBox = view.FindControl<TextBox>("CommandHistorySearchBox");
            Assert.NotNull(searchBox);
            Assert.Equal("git status", searchBox.Text);
            Assert.Equal(0, searchBox.SelectionStart);
            Assert.Equal(searchBox.Text!.Length, searchBox.SelectionEnd);
        });
    }

    [Fact]
    public async Task HistoryDrawerView_WhenArrowKeyNavigates_ClearsSearchBoxSelection()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            // Arrange
            var processService = new MockShellProcessService();
            var persistenceService = new MockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var tab = mainVm.SelectedTab;
            Assert.NotNull(tab);
            tab.CommandHistory.Add("cmd1");
            tab.CommandHistory.Add("cmd2");
            tab.FilteredCommandHistory.Add("cmd1");
            tab.FilteredCommandHistory.Add("cmd2");

            var view = new HistoryDrawerView
            {
                DataContext = mainVm
            };

            var searchBox = view.FindControl<TextBox>("CommandHistorySearchBox");
            Assert.NotNull(searchBox);
            searchBox.Text = "cmd";

            view.ShowHistoryDrawer();
            Dispatcher.UIThread.RunJobs();

            // Initially selected
            Assert.Equal(0, searchBox.SelectionStart);
            Assert.Equal(searchBox.Text!.Length, searchBox.SelectionEnd);

            // Act - Press Down key to navigate list
            var keyEventArgs = new KeyEventArgs
            {
                RoutedEvent = InputElement.KeyDownEvent,
                Key = Key.Down
            };
            view.HandleKeyDown(keyEventArgs);
            Dispatcher.UIThread.RunJobs();

            // Assert: selection is cleared, caret is at end
            Assert.Equal(searchBox.CaretIndex, searchBox.SelectionStart);
            Assert.Equal(searchBox.CaretIndex, searchBox.SelectionEnd);
        });
    }

    [Fact]
    public void TerminalTabViewModel_RequestFocusAndSelectHistorySearch_InvokesEvent()
    {
        // Arrange
        var session = new MockShellSession();
        var tab = new TerminalTabViewModel(session);
        var eventRaised = false;
        tab.FocusAndSelectHistorySearchRequested += () => eventRaised = true;

        // Act
        tab.RequestFocusAndSelectHistorySearch();

        // Assert
        Assert.True(eventRaised);
    }

    private class MockShellProcessService : IShellProcessService
    {
        public IShellSession CreateSession(string title, string? workingDirectory = null, ShellType shellType = ShellType.PowerShell, string? customExecutable = null, string? customArguments = null)
            => new MockShellSession { WorkingDirectory = workingDirectory };
    }

    private class MockTabStatePersistenceService : ITabStatePersistenceService
    {
        public Task SaveStateAsync(Models.WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Models.WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<Models.WorkspaceState?>(null);
    }

    private class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; } = "Test";
        public string? WorkingDirectory { get; set; } = @"C:\Test";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

        public event Action<byte[]>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }

        public void Start() { }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() { }
    }
}
