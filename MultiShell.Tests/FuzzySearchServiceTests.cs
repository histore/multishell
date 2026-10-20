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

    [Fact]
    public void IsMatch_LongTargetPath_PrefixMatchScoresHigherThanSubsequenceMatch()
    {
        // Arrange
        var service = new FuzzySearchService();
        var longPrefixMatch = "C:\\Projects\\VeryLongProjectDirectoryName\\SubFolder1\\SubFolder2\\SubFolder3\\Target";
        var shortSubsequenceMatch = "C:\\X\\T_a_r_g_e_t";

        // Act
        var hasPrefix = service.IsMatch("C:\\Projects", longPrefixMatch, out var prefixScore);
        var hasSubseq = service.IsMatch("C:\\Projects", shortSubsequenceMatch, out var subseqScore);

        // Assert
        Assert.True(hasPrefix);
        Assert.True(prefixScore > 0, "Prefix score must not be negative on long target strings.");
        Assert.True(prefixScore > subseqScore, "Prefix match must rank above loose subsequence match.");
    }

    [Fact]
    public void FilterAndRank_EqualScores_SortsBySecondaryKeyDescending()
    {
        // Arrange
        var service = new FuzzySearchService();
        var baseTime = DateTime.UtcNow;
        var items = new List<(string Name, DateTime LastUsed)>
        {
            ("git pull", baseTime.AddMinutes(-10)),
            ("git push", baseTime.AddMinutes(-2)),
            ("git init", baseTime.AddMinutes(-5))
        };

        // Act: All 3 match "git " with exact same length and score
        var results = service.FilterAndRank(
            items,
            "git ",
            x => x.Name,
            x => x.LastUsed,
            secondaryDescending: true).ToList();

        // Assert: Newest item must be first
        Assert.Equal(3, results.Count);
        Assert.Equal("git push", results[0].Name);
        Assert.Equal("git init", results[1].Name);
        Assert.Equal("git pull", results[2].Name);
    }

    [Fact]
    public void FilterAndRank_EmptyPattern_SortsBySecondaryKeyDescending()
    {
        // Arrange
        var service = new FuzzySearchService();
        var baseTime = DateTime.UtcNow;
        var items = new List<(string Name, DateTime LastUsed)>
        {
            ("cmd1", baseTime.AddMinutes(-10)),
            ("cmd2", baseTime.AddMinutes(-1)),
            ("cmd3", baseTime.AddMinutes(-5))
        };

        // Act
        var results = service.FilterAndRank(
            items,
            "",
            x => x.Name,
            x => x.LastUsed,
            secondaryDescending: true).ToList();

        // Assert: Sorted newest first
        Assert.Equal(3, results.Count);
        Assert.Equal("cmd2", results[0].Name);
        Assert.Equal("cmd3", results[1].Name);
        Assert.Equal("cmd1", results[2].Name);
    }

    [Fact]
    public void FilterAndRank_PrefixMatchesWithDifferentLengths_SortsNewerEntriesFirst()
    {
        // Arrange
        var service = new FuzzySearchService();
        var baseTime = DateTime.UtcNow;
        var items = new List<(string Command, DateTime LastUsed)>
        {
            ("git log", baseTime.AddMinutes(-10)),             // 7 chars, oldest
            ("git status", baseTime.AddMinutes(-1)),          // 10 chars, medium
            ("git commit -m \"fix\"", baseTime.AddSeconds(-5)) // 19 chars, newest
        };

        // Act: Filter by "git" prefix
        var results = service.FilterAndRank(
            items,
            "git",
            x => x.Command,
            x => x.LastUsed,
            secondaryDescending: true).ToList();

        // Assert: Equal match quality (prefix match) must prioritize recency over string length
        Assert.Equal(3, results.Count);
        Assert.Equal("git commit -m \"fix\"", results[0].Command);
        Assert.Equal("git status", results[1].Command);
        Assert.Equal("git log", results[2].Command);
    }

    [Fact]
    public void FilterAndRank_EqualMatchQualityAndEqualTimestamps_SortsShorterLengthFirst()
    {
        // Arrange
        var service = new FuzzySearchService();
        var exactTime = DateTime.UtcNow;
        var items = new List<(string Command, DateTime LastUsed)>
        {
            ("git status", exactTime),
            ("git log", exactTime)
        };

        // Act: Same timestamp, same prefix match
        var results = service.FilterAndRank(
            items,
            "git",
            x => x.Command,
            x => x.LastUsed,
            secondaryDescending: true).ToList();

        // Assert: When timestamps are identical, shorter length breaks tie
        Assert.Equal(2, results.Count);
        Assert.Equal("git log", results[0].Command);
        Assert.Equal("git status", results[1].Command);
    }

    [Fact]
    public void FilterAndRank_SubstringMatchesWithDifferentLengths_SortsNewerEntriesFirst()
    {
        // Arrange
        var service = new FuzzySearchService();
        var baseTime = DateTime.UtcNow;
        var items = new List<(string Directory, DateTime LastUsed)>
        {
            (@"C:\projekte\old", baseTime.AddDays(-5)),
            (@"C:\projekte\csharp\multishell\very\long\nested\path", baseTime.AddMinutes(-1))
        };

        // Act: Both contain "projekte" at path boundary
        var results = service.FilterAndRank(
            items,
            "projekte",
            x => x.Directory,
            x => x.LastUsed,
            secondaryDescending: true).ToList();

        // Assert: Newer path must be ranked above older shorter path
        Assert.Equal(2, results.Count);
        Assert.Equal(@"C:\projekte\csharp\multishell\very\long\nested\path", results[0].Directory);
        Assert.Equal(@"C:\projekte\old", results[1].Directory);
    }
}

