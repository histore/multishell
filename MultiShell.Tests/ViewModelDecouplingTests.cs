using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class ViewModelDecouplingTests
{
    private sealed class MockDispatcherService : IDispatcherService
    {
        public int PostCount { get; private set; }
        public int RunOnUiCount { get; private set; }
        public int InvokeAsyncCount { get; private set; }
        public bool IsAvailable { get; set; } = true;
        public bool CheckAccessResult { get; set; } = true;

        public bool CheckAccess() => CheckAccessResult;

        public void Post(Action action)
        {
            PostCount++;
            action();
        }

        public void RunOnUi(Action action)
        {
            RunOnUiCount++;
            action();
        }

        public Task InvokeAsync(Func<Task> action)
        {
            InvokeAsyncCount++;
            return action();
        }

        public IDispatcherTimer? CreateTimer(TimeSpan interval, Action tickHandler) =>
            IsAvailable ? new MockTimer() : null;

        private sealed class MockTimer : IDispatcherTimer
        {
            public void Start() { }
            public void Stop() { }
            public void Dispose() { }
        }
    }

    private sealed class MockTerminalModelAdapter : ITerminalModelAdapter
    {
        public List<string> FedTexts { get; } = [];
        public int ClearSelectionCount { get; private set; }
        public int SuppressMouseTrackingCount { get; private set; }
        public bool IsDisposed { get; private set; }

        public object NativeModel { get; } = new object();
        public int Cols => 80;
        public int Rows => 24;
        public bool IsAlternateBufferActive { get; set; }
        public bool BracketedPasteMode { get; set; }
        public bool HasSelection { get; set; }
        public string SelectedText { get; set; } = string.Empty;
        public int SearchResultCount { get; set; }
        public int CurrentSearchResultIndex { get; set; }
        public Action? UpdateUI { get; set; }

        public event EventHandler<ReadOnlyMemory<byte>>? UserInput;
        public event EventHandler<(int Cols, int Rows)>? SizeChanged;
        public event EventHandler? SearchStateChanged;

        public void Feed(string text) => FedTexts.Add(text);
        public void SuppressMouseTracking() => SuppressMouseTrackingCount++;
        public void ClearSelection()
        {
            HasSelection = false;
            SelectedText = string.Empty;
            ClearSelectionCount++;
        }
        public void PageUp() { }
        public void PageDown() { }
        public void Search(string query) { }
        public void SelectNextSearchResult() { }
        public void SelectPreviousSearchResult() { }
        public void RaiseUserInput(byte[] data) => UserInput?.Invoke(this, data);
        public void RaiseSizeChanged(int cols, int rows) => SizeChanged?.Invoke(this, (cols, rows));
        public void RaiseSearchStateChanged() => SearchStateChanged?.Invoke(this, EventArgs.Empty);

        public void Dispose() => IsDisposed = true;
    }

    private sealed class MockTerminalModelFactory : ITerminalModelFactory
    {
        public List<MockTerminalModelAdapter> CreatedAdapters { get; } = [];

        public ITerminalModelAdapter Create()
        {
            var adapter = new MockTerminalModelAdapter();
            CreatedAdapters.Add(adapter);
            return adapter;
        }
    }

    private sealed class StubShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title => "Stub";
        public string? WorkingDirectory => @"C:\projekte";
        public ShellType ShellType => ShellType.PowerShell;
        public bool IsRunning => true;

        public event Action<ReadOnlyMemory<byte>>? DataReceived { add { } remove { } }
        public event Action<int>? Exited { add { } remove { } }
        public event Action<string>? WorkingDirectoryChanged { add { } remove { } }
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted { add { } remove { } }

        public void Start() { }
        public void Send(byte[] data) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() { }
    }

    [Theory]
    [InlineData("#fff", true)]
    [InlineData("#7AA2F7", true)]
    [InlineData("#807AA2F7", true)]
    [InlineData("  #0E0F15  ", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("7AA2F7", false)]
    [InlineData("#12", false)]
    [InlineData("#GGGGGG", false)]
    public void IsValidHexColor_ValidatesExpectedFormats(string? input, bool expected)
    {
        Assert.Equal(expected, UiFrameworkInfo.IsValidHexColor(input));
    }

    [Fact]
    public void GetUiTerminalStack_ReturnsExpectedDescription()
    {
        var stack = UiFrameworkInfo.GetUiTerminalStack();

        Assert.Contains("Avalonia UI", stack);
        Assert.Contains("ConPTY", stack);
    }

    [Fact]
    public void TerminalTabViewModel_OperatesWithInjectedDispatcherAndTerminalAdapter()
    {
        var dispatcher = new MockDispatcherService();
        var factory = new MockTerminalModelFactory();
        var session = new StubShellSession();

        using var tab = new TerminalTabViewModel(
            session,
            dispatcherService: dispatcher,
            terminalModelFactory: factory);

        Assert.Single(factory.CreatedAdapters);
        var adapter = factory.CreatedAdapters[0];

        tab.ClearBuffer();
        Assert.Single(adapter.FedTexts);
        Assert.Equal(1, adapter.ClearSelectionCount);
        Assert.True(dispatcher.RunOnUiCount >= 1);

        tab.ActivePane!.SuppressMouseTrackingMode();
        Assert.Equal(1, adapter.SuppressMouseTrackingCount);

        adapter.SearchResultCount = 4;
        adapter.CurrentSearchResultIndex = 1;
        adapter.RaiseSearchStateChanged();
        Assert.Equal(4, tab.SearchResultCount);
        Assert.Equal(1, tab.CurrentSearchResultIndex);

        tab.Dispose();
        Assert.True(adapter.IsDisposed);
    }
}
