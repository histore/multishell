using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

[Collection("HeadlessUI")]
public class TerminalLinkHighlightPositionTests
{
    [Fact]
    public async Task PointerMoved_WithCtrl_OverLink_PositionsHoverHighlightPreciselyAligned()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var processService = new MockShellProcessService();
            var persistenceService = new MockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var paneVm = mainVm.SelectedTab!.ActivePane;
            Assert.NotNull(paneVm);

            var paneView = new TerminalPaneView
            {
                DataContext = paneVm,
                Width = 800,
                Height = 600
            };

            var window = new Window
            {
                Width = 800,
                Height = 600,
                Content = paneView
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var terminal = paneView.FindControl<SvcSystems.UI.Terminal.TerminalControl>("Terminal");
            var hoverBorder = paneView.FindControl<Border>("HoverLinkBorder");
            Assert.NotNull(terminal);
            Assert.NotNull(hoverBorder);

            // Feed link text into terminal
            paneVm.TerminalModel.Feed("Visit https://github.com/histore/multishell for documentation\r\n");
            Dispatcher.UIThread.RunJobs();

            var sizeField = terminal.GetType().GetField("_consoleTextSize", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            var consoleTextSize = (Size)sizeField!.GetValue(terminal)!;
            Assert.True(consoleTextSize.Width > 0 && consoleTextSize.Height > 0);

            // "Visit " is 6 chars -> start column is 6
            var targetX = 6 * consoleTextSize.Width + 5;
            var targetY = 0 * consoleTextSize.Height + 5;

            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Mouse, isPrimary: true);
            var pointerEvent = new PointerEventArgs(
                InputElement.PointerMovedEvent,
                terminal,
                pointer,
                terminal,
                new Point(targetX, targetY),
                (ulong)Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                KeyModifiers.Control);

            terminal.RaiseEvent(pointerEvent);
            Dispatcher.UIThread.RunJobs();

            // Assert highlight is visible
            Assert.True(hoverBorder.IsVisible);

            // Expected metric offsets:
            var expectedXOff = Math.Max(1.0, Math.Round(consoleTextSize.Width * 0.16, 1));
            var expectedYOff = Math.Max(1.5, Math.Round(consoleTextSize.Height * 0.12, 1));
            var expectedWOff = -(expectedXOff * 1.5);
            var expectedHOff = -expectedYOff;

            var expectedLeft = 6 * consoleTextSize.Width + expectedXOff;
            var expectedTop = 0 * consoleTextSize.Height + expectedYOff;
            var expectedWidth = "https://github.com/histore/multishell".Length * consoleTextSize.Width + expectedWOff;
            var expectedHeight = consoleTextSize.Height + expectedHOff;

            Assert.Equal(expectedLeft, Canvas.GetLeft(hoverBorder), precision: 2);
            Assert.Equal(expectedTop, Canvas.GetTop(hoverBorder), precision: 2);
            Assert.Equal(expectedWidth, hoverBorder.Width, precision: 2);
            Assert.Equal(expectedHeight, hoverBorder.Height, precision: 2);

            // Verify highlight is not positioned above the text line (Top > 0)
            Assert.True(Canvas.GetTop(hoverBorder) > 0);
            // Verify highlight does not bleed into line above or below
            Assert.True(Canvas.GetTop(hoverBorder) + hoverBorder.Height <= consoleTextSize.Height + 0.1);

            // Moving pointer away without Ctrl should hide highlight
            var pointerLeaveEvent = new PointerEventArgs(
                InputElement.PointerMovedEvent,
                terminal,
                pointer,
                terminal,
                new Point(targetX, targetY),
                (ulong)Environment.TickCount64,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.Other),
                KeyModifiers.None);

            terminal.RaiseEvent(pointerLeaveEvent);
            Dispatcher.UIThread.RunJobs();

            Assert.False(hoverBorder.IsVisible);
        });
    }

    [Fact]
    public async Task ShowLinkHighlight_DirectCall_SetsCanvasPositionAndSize()
    {
        await HeadlessTestSession.DispatchAsync(async () =>
        {
            var processService = new MockShellProcessService();
            var persistenceService = new MockTabStatePersistenceService();
            using var mainVm = new MainViewModel(processService, persistenceService, new ThemeService(), new LocalizationService(), new FontSizeService());
            await mainVm.InitializeWorkspaceAsync();

            var paneVm = mainVm.SelectedTab!.ActivePane;
            var paneView = new TerminalPaneView
            {
                DataContext = paneVm,
                Width = 800,
                Height = 600
            };

            var window = new Window { Width = 800, Height = 600, Content = paneView };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var hoverBorder = paneView.FindControl<Border>("HoverLinkBorder");
            Assert.NotNull(hoverBorder);

            paneView.ShowLinkHighlight(50.5, 25.0, 120.0, 18.0);
            Dispatcher.UIThread.RunJobs();

            Assert.True(hoverBorder.IsVisible);
            Assert.Equal(50.5, Canvas.GetLeft(hoverBorder));
            Assert.Equal(25.0, Canvas.GetTop(hoverBorder));
            Assert.Equal(120.0, hoverBorder.Width);
            Assert.Equal(18.0, hoverBorder.Height);
        });
    }

    private class MockShellProcessService : IShellProcessService
    {
        public IShellSession CreateSession(string title, string? workingDirectory = null, ShellType shellType = ShellType.PowerShell, string? customExecutable = null, string? customArguments = null)
            => new MockShellSession { WorkingDirectory = workingDirectory };
    }

    private class MockTabStatePersistenceService : ITabStatePersistenceService
    {
        public Task SaveStateAsync(WorkspaceState state, System.Threading.CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<WorkspaceState?> LoadStateAsync(System.Threading.CancellationToken cancellationToken = default) => Task.FromResult<WorkspaceState?>(null);
    }

    private class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; } = "Test";
        public string? WorkingDirectory { get; set; } = @"C:\Test";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted { add { } remove { } }

        public void Start() { }
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() { }
    }
}
