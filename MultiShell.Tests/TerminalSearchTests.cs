using System;
using System.Threading;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

/// <summary>
/// Unit tests for REQ-TERM-006: In-Terminal Text & Scrollback Search Overlay.
/// </summary>
public class TerminalSearchTests
{
    private readonly LocalizationService _loc = new("en", isUserSelection: true);

    [Fact]
    public void InitialState_SearchIsClosedAndQueryIsEmpty()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        Assert.False(vm.IsSearchOpen);
        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.Equal(string.Empty, vm.SearchMatchSummary);
        Assert.Equal(0, vm.SearchResultCount);
        Assert.Equal(0, vm.CurrentSearchResultIndex);
    }

    [Fact]
    public void OpenSearch_SetsIsSearchOpenAndFiresFocusEvent()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        bool focusRequested = false;
        vm.FocusSearchBoxRequested += () => focusRequested = true;

        vm.OpenSearch();

        Assert.True(vm.IsSearchOpen);
        Assert.True(focusRequested);
    }

    [Fact]
    public void OpenSearch_PrePopulatesQueryFromTerminalSelection()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        vm.TerminalModel.Feed("line one error: connection failed\r\nline two\r\n");
        vm.TerminalModel.SelectWordOrExpression(10, 0); // Select a word
        var selectedText = vm.TerminalModel.SelectedText;

        vm.OpenSearch();

        Assert.True(vm.IsSearchOpen);
        if (!string.IsNullOrWhiteSpace(selectedText))
        {
            Assert.Equal(selectedText.Trim(), vm.SearchQuery);
        }
    }

    [Fact]
    public void CloseSearch_SetsIsSearchOpenFalse_ClearsSearch_AndFiresFocusTerminalEvent()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        bool focusTerminalRequested = false;
        vm.FocusTerminalRequested += () => focusTerminalRequested = true;

        vm.OpenSearch();
        vm.SearchQuery = "error";
        Assert.True(vm.IsSearchOpen);

        vm.CloseSearch();

        Assert.False(vm.IsSearchOpen);
        Assert.Equal(string.Empty, vm.SearchQuery);
        Assert.Equal(string.Empty, vm.SearchMatchSummary);
        Assert.True(focusTerminalRequested);
    }

    [Fact]
    public void ToggleSearch_TogglesSearchState()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        vm.ToggleSearch();
        Assert.True(vm.IsSearchOpen);

        vm.ToggleSearch();
        Assert.False(vm.IsSearchOpen);
    }

    [Fact]
    public void SearchQuery_MatchesTextInBuffer_AndUpdatesCounts()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        vm.TerminalModel.Feed("alpha beta gamma alpha delta alpha\r\n");
        vm.SearchQuery = "alpha";

        Assert.Equal(3, vm.SearchResultCount);
        Assert.Contains("1 of 3", vm.SearchMatchSummary);
    }

    [Fact]
    public void SearchQuery_WhenNoMatches_ShowsNoResultsSummary()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        vm.TerminalModel.Feed("alpha beta gamma\r\n");
        vm.SearchQuery = "nonexistent";

        Assert.Equal(0, vm.SearchResultCount);
        Assert.Equal("No results", vm.SearchMatchSummary);
    }

    [Fact]
    public void SearchNext_And_SearchPrevious_CycleThroughMatches()
    {
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: _loc);

        vm.TerminalModel.Feed("match1 match2 match3\r\n");
        vm.SearchQuery = "match";

        Assert.Equal(3, vm.SearchResultCount);
        Assert.Equal(0, vm.CurrentSearchResultIndex);

        vm.SearchNext();
        Assert.Equal(1, vm.CurrentSearchResultIndex);
        Assert.Contains("2 of 3", vm.SearchMatchSummary);

        vm.SearchNext();
        Assert.Equal(2, vm.CurrentSearchResultIndex);
        Assert.Contains("3 of 3", vm.SearchMatchSummary);

        vm.SearchPrevious();
        Assert.Equal(1, vm.CurrentSearchResultIndex);
        Assert.Contains("2 of 3", vm.SearchMatchSummary);
    }

    [Fact]
    public void SearchSummary_LocalizesInGerman()
    {
        var locDe = new LocalizationService("de", isUserSelection: true);
        var session = new MockPowerShellSession("Test Tab");
        using var vm = new TerminalTabViewModel(session, localizationService: locDe);

        vm.TerminalModel.Feed("test eins test zwei\r\n");
        vm.SearchQuery = "test";

        Assert.Equal(2, vm.SearchResultCount);
        Assert.Equal("1 von 2", vm.SearchMatchSummary);

        vm.SearchQuery = "keintreffer";
        Assert.Equal(0, vm.SearchResultCount);
        Assert.Equal("Keine Treffer", vm.SearchMatchSummary);
    }

    private class MockPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; }
        public bool Disposed { get; private set; }
        public bool Started { get; private set; }

#pragma warning disable CS0067
        public event Action<byte[]>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted;
#pragma warning restore CS0067

        public MockPowerShellSession(string title, string? workingDirectory = null)
        {
            Title = title;
            WorkingDirectory = workingDirectory;
        }

        public void Start() => IsRunning = true;
        public void Send(byte[] input) { }
        public void Resize(int cols, int rows) { }
        public void Dispose() => Disposed = true;
    }
}
