using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Avalonia.VisualTree;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

[Collection("HeadlessUI")]
public class ThemeServiceTests
{
    [Fact]
    public async Task ThemeService_SetTerminalTheme_UpdatesThemeAndResources()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var service = new ThemeService();

            service.SetTerminalTheme(false);
            Assert.False(service.IsDarkTerminalTheme);

            service.SetTerminalTheme(true);
            Assert.True(service.IsDarkTerminalTheme);
        });
    }

    [Fact]
    public async Task ThemeService_ToggleTerminalTheme_InvertsTheme()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var service = new ThemeService();
            bool initial = service.IsDarkTerminalTheme;

            service.ToggleTerminalTheme();
            Assert.Equal(!initial, service.IsDarkTerminalTheme);

            service.ToggleTerminalTheme();
            Assert.Equal(initial, service.IsDarkTerminalTheme);
        });
    }

    [Fact]
    public async Task ThemeService_SetAppTheme_UpdatesApplicationThemeVariant()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var service = new ThemeService();

            service.SetAppTheme(false);
            if (Application.Current != null)
            {
                Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            }

            service.SetAppTheme(true);
            if (Application.Current != null)
            {
                Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
            }
        });
    }

    [Fact]
    public async Task MainViewModel_ToggleTerminalTheme_UpdatesActiveTerminalPaneViewBrushesAndPalette()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var themeService = new ThemeService();
            using var mainVm = new MainViewModel(
                new MockShellProcessService(),
                new MockTabStatePersistenceService(),
                themeService,
                new LocalizationService(),
                new FontSizeService());

            var session = new MockShellSession { WorkingDirectory = @"C:\Test" };
            var tab = new TerminalTabViewModel(session);
            mainVm.Tabs.Add(tab);
            mainVm.SelectedTab = tab;

            var pane = tab.Panes[0];
            var paneView = new Views.TerminalPaneView
            {
                DataContext = pane
            };

            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Initial: Dark theme
            Assert.True(mainVm.IsDarkTerminalTheme);
            Assert.True(tab.IsDarkTerminalTheme);
            Assert.True(pane.IsDarkTerminalTheme);

            // Act: Toggle Terminal Theme to Light
            mainVm.ToggleTerminalThemeCommand.Execute(null);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();

            // Assert: ViewModels updated
            Assert.False(mainVm.IsDarkTerminalTheme);
            Assert.False(tab.IsDarkTerminalTheme);
            Assert.False(pane.IsDarkTerminalTheme);

            // Assert: View updated
            var bgBrush = paneView.Terminal.Background as Avalonia.Media.ISolidColorBrush;
            Assert.NotNull(bgBrush);
            Assert.Equal(Avalonia.Media.Color.Parse("#F8F9FC"), bgBrush.Color);

            var scope = paneView.TerminalScope;
            Assert.NotNull(scope);
            Assert.Equal(ThemeVariant.Light, scope.RequestedThemeVariant);

            var color0 = paneView.Terminal.Resources["SvcSystems.UI.TerminalColor0"] as Avalonia.Media.ISolidColorBrush;
            var color15 = paneView.Terminal.Resources["SvcSystems.UI.TerminalColor15"] as Avalonia.Media.ISolidColorBrush;
            Assert.NotNull(color0);
            Assert.NotNull(color15);
            Assert.Equal(Avalonia.Media.Color.Parse("#F8F9FC"), color0.Color);
            Assert.Equal(Avalonia.Media.Color.Parse("#0A0B10"), color15.Color);
        });
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

        public event System.Action<System.ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event System.Action<int>? Exited { add { } remove { } }
        public event System.Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event System.Action<string>? CommandExecuted { add { } remove { } }
        public event System.Action<int>? CommandCompleted { add { } remove { } }

        public void Start() { }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() { }
    }
}

