using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

[Collection("HeadlessUI")]
public class TabBarDoubleClickTests
{
    [Fact]
    public async Task Button_OnPointerPressed_WithClickCount2_IsHandled()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var button = new Button { Content = "‹" };
            var window = new Window { Content = button };
            window.Show();

            bool pointerPressedFired = false;
            bool handledInHandler = false;

            button.AddHandler(InputElement.PointerPressedEvent, (s, e) =>
            {
                pointerPressedFired = true;
                handledInHandler = e.Handled;
            }, Avalonia.Interactivity.RoutingStrategies.Bubble, handledEventsToo: true);

            // Simulate pointer pressed with ClickCount = 2
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var pointerEvent = new PointerPressedEventArgs(
                button,
                pointer,
                window,
                new Point(5, 5),
                (ulong)System.Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None,
                clickCount: 2);

            button.RaiseEvent(pointerEvent);

            Assert.True(pointerPressedFired);
            Assert.True(pointerEvent.Handled);
        });
    }

    [Fact]
    public async Task EnabledButton_DoubleClick_DoesNotCreateTab()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var processService = new LocalMockShellProcessService();
            var persistenceService = new LocalMockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var window = new MainWindow
            {
                DataContext = mainVm
            };
            window.Show();

            Assert.Single(mainVm.Tabs);

            var leftBtn = window.FindControl<Button>("TabScrollLeftBtn");
            Assert.NotNull(leftBtn);
            leftBtn.IsVisible = true;
            leftBtn.IsEnabled = true;

            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var pointerEvent = new PointerPressedEventArgs(
                leftBtn,
                pointer,
                window,
                new Point(5, 5),
                (ulong)System.Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None,
                clickCount: 2);

            leftBtn.RaiseEvent(pointerEvent);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(mainVm.Tabs);
        });
    }

    [Fact]
    public async Task DisabledButton_ClickOnLeftScrollButton_DoesNotCreateTab()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var processService = new LocalMockShellProcessService();
            var persistenceService = new LocalMockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var window = new MainWindow
            {
                DataContext = mainVm
            };
            window.Show();

            Assert.Single(mainVm.Tabs);

            var leftBtn = window.FindControl<Button>("TabScrollLeftBtn");
            var tabBarContainer = window.FindControl<Grid>("TabBarContainer");
            Assert.NotNull(leftBtn);
            Assert.NotNull(tabBarContainer);

            leftBtn.IsVisible = true;
            leftBtn.IsEnabled = false;

            // When disabled, pointer press falls through to TabBarContainer at the position of the left button
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var pointerEvent = new PointerPressedEventArgs(
                tabBarContainer,
                pointer,
                window,
                new Point(5, 5),
                (ulong)System.Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None,
                clickCount: 2);

            tabBarContainer.RaiseEvent(pointerEvent);
            Dispatcher.UIThread.RunJobs();

            // Must NOT create a new tab
            Assert.Single(mainVm.Tabs);
        });
    }

    [Fact]
    public async Task DisabledButton_ClickOnRightScrollButton_DoesNotCreateTab()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var processService = new LocalMockShellProcessService();
            var persistenceService = new LocalMockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var window = new MainWindow
            {
                DataContext = mainVm
            };
            window.Show();

            Assert.Single(mainVm.Tabs);

            var rightBtn = window.FindControl<Button>("TabScrollRightBtn");
            var tabBarContainer = window.FindControl<Grid>("TabBarContainer");
            Assert.NotNull(rightBtn);
            Assert.NotNull(tabBarContainer);

            rightBtn.IsVisible = true;
            rightBtn.IsEnabled = false;

            // When disabled, pointer press falls through to TabBarContainer at the position of the right button
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var pointerEvent = new PointerPressedEventArgs(
                tabBarContainer,
                pointer,
                window,
                new Point(rightBtn.Bounds.Center.X, rightBtn.Bounds.Center.Y),
                (ulong)System.Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None,
                clickCount: 2);

            tabBarContainer.RaiseEvent(pointerEvent);
            Dispatcher.UIThread.RunJobs();

            // Must NOT create a new tab
            Assert.Single(mainVm.Tabs);
        });
    }

    [Fact]
    public async Task DoubleClick_InEmptyTabsArea_CreatesNewTab()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var processService = new LocalMockShellProcessService();
            var persistenceService = new LocalMockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var window = new MainWindow
            {
                DataContext = mainVm
            };
            window.Show();

            Assert.Single(mainVm.Tabs);

            var scrollViewer = window.FindControl<ScrollViewer>("TabsScrollViewer");
            var tabBarContainer = window.FindControl<Grid>("TabBarContainer");
            Assert.NotNull(scrollViewer);
            Assert.NotNull(tabBarContainer);

            // Simulate double-click on free space of TabsScrollViewer (e.g. at (200, 10))
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var pointerEvent = new PointerPressedEventArgs(
                scrollViewer,
                pointer,
                window,
                new Point(200, 10),
                (ulong)System.Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonPressed),
                KeyModifiers.None,
                clickCount: 2);

            tabBarContainer.RaiseEvent(pointerEvent);
            Dispatcher.UIThread.RunJobs();

            // Should create a tab
            Assert.Equal(2, mainVm.Tabs.Count);
        });
    }

    private class LocalMockShellProcessService : IShellProcessService
    {
        public IShellSession CreateSession(string title, string? workingDirectory = null, ShellType shellType = ShellType.PowerShell, string? customExecutable = null, string? customArguments = null)
            => new LocalMockShellSession { WorkingDirectory = workingDirectory };
    }

    private class LocalMockTabStatePersistenceService : ITabStatePersistenceService
    {
        public Task SaveStateAsync(Models.WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Models.WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<Models.WorkspaceState?>(null);
    }

    private class LocalMockShellSession : IShellSession
    {
        public System.Guid SessionId { get; } = System.Guid.NewGuid();
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
