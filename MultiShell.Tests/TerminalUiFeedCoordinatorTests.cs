using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class TerminalUiFeedCoordinatorTests
{
    private sealed class RecordingDispatcherService : IDispatcherService
    {
        public bool HasAccess { get; set; }
        public bool IsAvailable => true;
        public Queue<Action> PostedActions { get; } = new();

        public bool CheckAccess() => HasAccess;
        public void Post(Action action) => PostedActions.Enqueue(action);
        public void RunOnUi(Action action)
        {
            if (HasAccess) action();
            else PostedActions.Enqueue(action);
        }
        public Task InvokeAsync(Func<Task> action) => action();
        public IDispatcherTimer? CreateTimer(TimeSpan interval, Action tickHandler) => null;

        public void DrainAll()
        {
            while (PostedActions.Count > 0)
            {
                PostedActions.Dequeue().Invoke();
            }
        }
    }

    private sealed class FakeTerminalModel : ITerminalModelAdapter
    {
        public List<string> FedChunks { get; } = new();
        public object NativeModel => this;
        public int Cols => 80;
        public int Rows => 24;
        public bool IsAlternateBufferActive => false;
        public bool BracketedPasteMode => false;
        public bool HasSelection => false;
        public string SelectedText => string.Empty;
        public int SearchResultCount => 0;
        public int CurrentSearchResultIndex => 0;
        public Action? UpdateUI { get; set; }

#pragma warning disable CS0067
        public event EventHandler<ReadOnlyMemory<byte>>? UserInput;
        public event EventHandler<(int Cols, int Rows)>? SizeChanged;
        public event EventHandler? SearchStateChanged;
#pragma warning restore CS0067

        public void Feed(string text) => FedChunks.Add(text);
        public void SuppressMouseTracking() { }
        public void ClearSelection() { }
        public void PageUp() { }
        public void PageDown() { }
        public void Search(string query) { }
        public void SelectNextSearchResult() { }
        public void SelectPreviousSearchResult() { }
        public void Dispose() { }
    }

    private sealed class StubSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title => "Stub";
        public string? WorkingDirectory => @"C:\";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning => true;
        public bool ThrowOnSend { get; set; }
        public List<string> SentResponses { get; } = new();

#pragma warning disable CS0067
        public event Action<ReadOnlyMemory<byte>>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
        public event Action<int>? CommandCompleted;
#pragma warning restore CS0067

        public void Start() { }
        public void Send(byte[] input)
        {
            if (ThrowOnSend) throw new IOException("Pipe closed");
            SentResponses.Add(Encoding.ASCII.GetString(input));
        }
        public void Resize(int cols, int rows) { }
        public void Dispose() { }
    }

    [Fact]
    public void ProcessAndFeed_FromBackgroundThread_CoalescesMultipleChunksIntoSingleUiFlush()
    {
        using var coordinator = new TerminalUiFeedCoordinator();
        var dispatcher = new RecordingDispatcherService { HasAccess = false };
        var model = new FakeTerminalModel();
        var session = new StubSession();

        coordinator.ProcessAndFeed("chunk1-"u8, true, session, model, dispatcher);
        coordinator.ProcessAndFeed("chunk2-"u8, true, session, model, dispatcher);
        coordinator.ProcessAndFeed("chunk3"u8, true, session, model, dispatcher);

        Assert.Single(dispatcher.PostedActions);
        Assert.Empty(model.FedChunks);

        dispatcher.DrainAll();

        var fed = Assert.Single(model.FedChunks);
        Assert.Equal("chunk1-chunk2-chunk3", fed);
    }

    [Fact]
    public void ProcessAndFeed_UiThreadCallAfterQueuedBackgroundChunk_PreservesStrictOrder()
    {
        using var coordinator = new TerminalUiFeedCoordinator();
        var dispatcher = new RecordingDispatcherService { HasAccess = false };
        var model = new FakeTerminalModel();
        var session = new StubSession();

        // Background chunk queued first
        coordinator.ProcessAndFeed("first-"u8, true, session, model, dispatcher);
        Assert.Single(dispatcher.PostedActions);

        // UI-thread call arrives before posted flush executes
        dispatcher.HasAccess = true;
        coordinator.ProcessAndFeed("second"u8, true, session, model, dispatcher);

        var fed = Assert.Single(model.FedChunks);
        Assert.Equal("first-second", fed);

        // Posted flush becomes a clean no-op
        dispatcher.DrainAll();
        Assert.Single(model.FedChunks);
    }

    [Fact]
    public void Flush_AfterLargeBurst_TrimsPendingBufferCapacity()
    {
        var sharedBuffer = new StringBuilder();
        using var coordinator = new TerminalUiFeedCoordinator(sharedBuffer);
        var dispatcher = new RecordingDispatcherService { HasAccess = false };
        var model = new FakeTerminalModel();
        var session = new StubSession();

        var largePayload = Encoding.UTF8.GetBytes(new string('A', 80_000));
        coordinator.ProcessAndFeed(largePayload, true, session, model, dispatcher);
        Assert.True(sharedBuffer.Capacity > 65536);

        dispatcher.DrainAll();

        Assert.Equal(4096, sharedBuffer.Capacity);
        Assert.Single(model.FedChunks);
        Assert.Equal(80_000, model.FedChunks[0].Length);
    }

    [Fact]
    public void Dispose_ClearsPendingBufferAndSuppressesPendingFlushes()
    {
        var sharedBuffer = new StringBuilder();
        var coordinator = new TerminalUiFeedCoordinator(sharedBuffer);
        var dispatcher = new RecordingDispatcherService { HasAccess = false };
        var model = new FakeTerminalModel();
        var session = new StubSession();

        coordinator.ProcessAndFeed("pending"u8, true, session, model, dispatcher);
        coordinator.Dispose();

        // Draining already-posted Flush must not touch terminalModel after Dispose
        dispatcher.DrainAll();
        coordinator.ProcessAndFeed("after-dispose"u8, true, session, model, dispatcher);

        Assert.Empty(model.FedChunks);
        Assert.Equal(0, sharedBuffer.Length);
    }

    [Fact]
    public void ProcessAndFeed_OscColorQuery_RespondsAndSwallowsClosedPipeErrors()
    {
        using var coordinator = new TerminalUiFeedCoordinator();
        var dispatcher = new RecordingDispatcherService { HasAccess = true };
        var model = new FakeTerminalModel();
        var session = new StubSession();

        coordinator.ProcessAndFeed("\x1b]11;?\x07"u8, isDarkTheme: true, session, model, dispatcher);
        Assert.Single(session.SentResponses);
        Assert.Contains("rgb:0e0e/0f0f/1515", session.SentResponses[0]);

        session.ThrowOnSend = true;
        var ex = Record.Exception(() =>
            coordinator.ProcessAndFeed("\x1b]10;?\x07"u8, isDarkTheme: false, session, model, dispatcher));
        Assert.Null(ex);
    }
}
