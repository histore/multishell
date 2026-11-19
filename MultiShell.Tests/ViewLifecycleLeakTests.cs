using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

[Collection("HeadlessUI")]
public class ViewLifecycleLeakTests
{
    private sealed class StubSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title => "Stub";
        public string? WorkingDirectory => @"C:\";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning { get; private set; } = true;

#pragma warning disable CS0067
        public event Action<ReadOnlyMemory<byte>>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;
#pragma warning restore CS0067

        public void Start() => IsRunning = true;
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() => IsRunning = false;
    }

    [Fact]
    public async Task SplitPaneContainerControl_RebuildAndDetach_DoesNotLeakSplitHandlersAndReattachesCleanly()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            using var tab = new TerminalTabViewModel(new StubSession());
            var splitPane = tab.SplitPane(tab.ActivePane, PaneSplitDirection.Vertical, new StubSession());

            var container = new SplitPaneContainerControl { DataContext = tab };
            var hostWindow = new Window { Content = container, Width = 800, Height = 500 };
            hostWindow.Show();

            try
            {
                Assert.True(container.IsSubscribedToTabLayout);
                Assert.Equal(1, container.ActiveSplitSubscriptionCount);
                Assert.Equal(2, container.CachedViewCount);

                // Rebuilding repeatedly must replace, not accumulate, SplitRatio subscriptions
                container.RebuildVisualTree();
                container.RebuildVisualTree();
                Assert.Equal(1, container.ActiveSplitSubscriptionCount);

                // Closing a pane must evict its cached view and clear DataContext
                tab.ClosePane(splitPane);
                container.RebuildVisualTree();
                Assert.Equal(0, container.ActiveSplitSubscriptionCount);
                Assert.Equal(1, container.CachedViewCount);

                // Detach from visual tree must clear layout and split subscriptions
                hostWindow.Content = null;
                Assert.False(container.IsSubscribedToTabLayout);
                Assert.Equal(0, container.ActiveSplitSubscriptionCount);

                // Re-attaching must restore LayoutChanged subscription
                hostWindow.Content = container;
                Assert.True(container.IsSubscribedToTabLayout);
            }
            finally
            {
                hostWindow.Close();
            }
        });
    }
}
