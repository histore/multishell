using System;
using System.Collections.Generic;
using System.Linq;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class FuzzySearchServiceTests
{
    private class MockPowerShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; } = "PS Test";
        public string? WorkingDirectory { get; set; } = @"C:\projekte\multishell";
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
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

    [Fact]
    public void IsMatch_ExactMatch_ReturnsTrueWithHighScore()
    {
        // Arrange
        var service = new FuzzySearchService();

        // Act
        var isMatch = service.IsMatch("git status", "git status", out var score);

        // Assert
        Assert.True(isMatch);
        Assert.True(score >= 1000);
    }

    [Fact]
    public void IsMatch_Subsequence_MatchesAbbreviatedCommands()
    {
        // Arrange
        var service = new FuzzySearchService();

        // Act & Assert
        Assert.True(service.IsMatch("gco", "git checkout main", out var score1));
        Assert.True(service.IsMatch("drun", "dotnet run --project MultiShell.csproj", out var score2));
        Assert.True(service.IsMatch("prj", @"C:\projekte\csharp\multishell", out var score3));
        Assert.True(score1 > 0);
        Assert.True(score2 > 0);
        Assert.True(score3 > 0);
    }

    [Fact]
    public void IsMatch_NonMatchingPattern_ReturnsFalse()
    {
        // Arrange
        var service = new FuzzySearchService();

        // Act
        var isMatch = service.IsMatch("xyz", "git checkout", out var score);

        // Assert
        Assert.False(isMatch);
        Assert.Equal(0, score);
    }

    [Fact]
    public void FilterAndRank_OrdersPrefixAndCloserMatchesAboveSparseMatches()
    {
        // Arrange
        var service = new FuzzySearchService();
        var commands = new List<string>
        {
            "dotnet run --configuration Release",
            "docker run -it ubuntu",
            "dotnet run",
            "git diff"
        };

        // Act
        var results = service.FilterAndRank(commands, "dotnet run", x => x).ToList();

        // Assert
        Assert.Equal(2, results.Count);
        Assert.Equal("dotnet run", results[0]);
        Assert.Equal("dotnet run --configuration Release", results[1]);
    }

    [Fact]
    public void TerminalTabViewModel_CommandFilterQuery_FiltersHistoryLive()
    {
        // Arrange
        var session = new MockPowerShellSession();
        using var tabVm = new TerminalTabViewModel(session);

        tabVm.RestoreHistory(
            new[] { "git status", "dotnet run", "git checkout -b feature", "clear" },
            new[] { @"C:\projekte\multishell", @"C:\projekte\other" });

        Assert.Equal(4, tabVm.FilteredCommandHistory.Count);

        // Act
        tabVm.CommandFilterQuery = "git";

        // Assert
        Assert.Equal(2, tabVm.FilteredCommandHistory.Count);
        Assert.Contains("git status", tabVm.FilteredCommandHistory);
        Assert.Contains("git checkout -b feature", tabVm.FilteredCommandHistory);

        // Act: Fuzzy query
        tabVm.CommandFilterQuery = "gco";
        Assert.Single(tabVm.FilteredCommandHistory);
        Assert.Equal("git checkout -b feature", tabVm.FilteredCommandHistory[0]);
    }

    [Fact]
    public void TerminalTabViewModel_DirectoryFilterQuery_FiltersDirectoriesLive()
    {
        // Arrange
        var session = new MockPowerShellSession();
        using var tabVm = new TerminalTabViewModel(session);

        tabVm.RestoreHistory(
            new[] { "dir" },
            new[] { @"C:\projekte\csharp\multishell", @"C:\Users\heino\Downloads", @"C:\projekte\rust\app" });

        // Act
        tabVm.DirectoryFilterQuery = "csharp";

        // Assert
        Assert.Single(tabVm.FilteredDirectoryHistory);
        Assert.Equal(@"C:\projekte\csharp\multishell", tabVm.FilteredDirectoryHistory[0]);
    }
}
