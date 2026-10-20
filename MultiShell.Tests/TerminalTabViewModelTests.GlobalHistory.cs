using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public partial class TerminalTabViewModelTests
{
    [Fact]
    public void GlobalHistory_ContainsCommandsAndDirectories_WithDistinctBadges()
    {
        // Arrange
        var historyService = new PathCommandHistoryService();
        var dirService = new DirectoryHistoryService();
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session, pathCommandHistoryService: historyService, directoryHistoryService: dirService);
        vm.StartSession();

        // Act: Record commands and directories
        historyService.RecordCommand(@"C:\projekte\app", "dotnet build");
        dirService.RecordDirectory(@"C:\projekte\app");
        dirService.RecordDirectory(@"C:\projekte\other");

        // Assert: GlobalHistory contains both command and directory items with badges
        Assert.Contains(vm.GlobalHistory, item => item.IsCommand && item.Text == "dotnet build" && item.BadgeText == "CMD");
        Assert.Contains(vm.GlobalHistory, item => item.IsDirectory && item.Text == @"C:\projekte\app" && item.BadgeText == "DIR");
        Assert.Contains(vm.GlobalHistory, item => item.IsDirectory && item.Text == @"C:\projekte\other" && item.BadgeText == "DIR");
    }

    [Fact]
    public void GlobalHistory_FilterQuery_FuzzyMatchesCommandOrDirectory()
    {
        // Arrange
        var historyService = new PathCommandHistoryService();
        var dirService = new DirectoryHistoryService();
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session, pathCommandHistoryService: historyService, directoryHistoryService: dirService);
        vm.StartSession();

        historyService.RecordCommand(@"C:\projekte\app", "git status");
        historyService.RecordCommand(@"C:\projekte\app", "dotnet run");
        dirService.RecordDirectory(@"C:\projekte\git-repo");

        // Act: Filter by "git"
        vm.GlobalFilterQuery = "git";

        // Assert: FilteredGlobalHistory has "git status" and "C:\projekte\git-repo", but not "dotnet run"
        Assert.Contains(vm.FilteredGlobalHistory, item => item.Text == "git status");
        Assert.Contains(vm.FilteredGlobalHistory, item => item.Text == @"C:\projekte\git-repo");
        Assert.DoesNotContain(vm.FilteredGlobalHistory, item => item.Text == "dotnet run");
    }

    [Fact]
    public void ExecuteGlobalItem_Command_SendsCommandToSessionWithReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();
        var item = new GlobalHistoryItem("git push", GlobalHistoryItemType.Command);

        // Act
        vm.ExecuteGlobalItem(item);

        // Assert
        Assert.NotEmpty(session.SentData);
        var sent = Encoding.UTF8.GetString(session.SentData[^1]);
        Assert.Equal("git push\r", sent);
    }

    [Fact]
    public void ExecuteGlobalItem_Directory_SendsNavigationToSessionWithReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();
        var item = new GlobalHistoryItem(@"C:\projekte\other", GlobalHistoryItemType.Directory);

        // Act
        vm.ExecuteGlobalItem(item);

        // Assert: PowerShell sends Set-Location
        Assert.NotEmpty(session.SentData);
        var sent = Encoding.UTF8.GetString(session.SentData[^1]);
        Assert.Equal("Set-Location -LiteralPath \"C:\\projekte\\other\"\r", sent);
    }

    [Fact]
    public void PasteGlobalItem_Command_PastesWithoutReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();
        var item = new GlobalHistoryItem("npm start", GlobalHistoryItemType.Command);

        // Act
        vm.PasteGlobalItem(item);

        // Assert
        Assert.NotEmpty(session.SentData);
        var sent = Encoding.UTF8.GetString(session.SentData[^1]);
        Assert.Equal("npm start", sent);
        Assert.False(sent.EndsWith("\r"));
    }

    [Fact]
    public void PasteGlobalItem_Directory_PastesNavigationWithoutReturn()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();
        var item = new GlobalHistoryItem(@"C:\projekte\other", GlobalHistoryItemType.Directory);

        // Act
        vm.PasteGlobalItem(item);

        // Assert
        Assert.NotEmpty(session.SentData);
        var sent = Encoding.UTF8.GetString(session.SentData[^1]);
        Assert.Equal("Set-Location -LiteralPath \"C:\\projekte\\other\"", sent);
        Assert.False(sent.EndsWith("\r"));
    }

    [Fact]
    public void GlobalSearch_EqualMatchScore_RanksMostRecentlyUsedFirst()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Simulate two commands with the exact same prefix/score for "git"
        session.SimulateCommandExecuted("git pull");
        session.SimulateCommandExecuted("git push");

        // Act: search for "git"
        vm.GlobalFilterQuery = "git";

        // Assert: both match, but "git push" was executed after "git pull", so "git push" is first
        Assert.True(vm.FilteredGlobalHistory.Count >= 2);
        var pushIndex = vm.FilteredGlobalHistory.ToList().FindIndex(x => x.Text == "git push");
        var pullIndex = vm.FilteredGlobalHistory.ToList().FindIndex(x => x.Text == "git pull");
        Assert.True(pushIndex >= 0 && pullIndex >= 0);
        Assert.True(pushIndex < pullIndex, "Most recently executed command should rank before older command with identical score.");
    }

    [Fact]
    public void GlobalSearch_CommandAndDirectoryWithEqualScore_RanksMostRecentlyUsedFirst()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // 1. Command executed first with target @"C:\tool"
        session.SimulateCommandExecuted(@"C:\tool");

        // 2. Directory visited second with target @"C:\tool"
        session.SimulateDirectoryChange(@"C:\tool");

        // Act: search for "C:\tool" (both produce exact match with score 1000)
        vm.GlobalFilterQuery = @"C:\tool";

        // Assert: directory was visited more recently than command, so directory is ranked above command
        Assert.True(vm.FilteredGlobalHistory.Count >= 2);
        var dirIndex = vm.FilteredGlobalHistory.ToList().FindIndex(x => x.IsDirectory && x.Text == @"C:\tool");
        var cmdIndex = vm.FilteredGlobalHistory.ToList().FindIndex(x => x.IsCommand && x.Text == @"C:\tool");
        Assert.True(dirIndex >= 0 && cmdIndex >= 0);
        Assert.True(dirIndex < cmdIndex, "Most recently visited directory should rank above older command with equal score.");
    }

    [Fact]
    public void CommandHistory_And_FilteredCommandHistory_AreSortedNewestToOldest()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: execute commands sequentially
        session.SimulateCommandExecuted("first command");
        session.SimulateCommandExecuted("second command");
        session.SimulateCommandExecuted("third command");

        // Assert: both CommandHistory and FilteredCommandHistory have newest at index 0
        Assert.Equal(3, vm.CommandHistory.Count);
        Assert.Equal("third command", vm.CommandHistory[0]);
        Assert.Equal("second command", vm.CommandHistory[1]);
        Assert.Equal("first command", vm.CommandHistory[2]);

        Assert.Equal(3, vm.FilteredCommandHistory.Count);
        Assert.Equal("third command", vm.FilteredCommandHistory[0]);
        Assert.Equal("second command", vm.FilteredCommandHistory[1]);
        Assert.Equal("first command", vm.FilteredCommandHistory[2]);
    }

    [Fact]
    public void DirectoryHistory_And_FilteredDirectoryHistory_AreSortedNewestToOldest()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\start");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Act: visit directories sequentially
        session.SimulateDirectoryChange(@"C:\projekte\dir1");
        session.SimulateDirectoryChange(@"C:\projekte\dir2");
        session.SimulateDirectoryChange(@"C:\projekte\dir3");

        // Assert: DirectoryHistory and FilteredDirectoryHistory have newest at index 0
        Assert.Equal(@"C:\projekte\dir3", vm.DirectoryHistory[0]);
        Assert.Equal(@"C:\projekte\dir2", vm.DirectoryHistory[1]);
        Assert.Equal(@"C:\projekte\dir1", vm.DirectoryHistory[2]);

        Assert.Equal(@"C:\projekte\dir3", vm.FilteredDirectoryHistory[0]);
        Assert.Equal(@"C:\projekte\dir2", vm.FilteredDirectoryHistory[1]);
        Assert.Equal(@"C:\projekte\dir1", vm.FilteredDirectoryHistory[2]);
    }

    [Fact]
    public void CommandSearch_EqualMatchScore_RanksMostRecentlyUsedFirst()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Two commands with identical prefix and length matching "git"
        session.SimulateCommandExecuted("git pull");
        session.SimulateCommandExecuted("git push");

        // Act: search for "git"
        vm.CommandFilterQuery = "git";

        // Assert: "git push" is newer, so it should rank before "git pull"
        Assert.True(vm.FilteredCommandHistory.Count >= 2);
        var pushIndex = vm.FilteredCommandHistory.IndexOf("git push");
        var pullIndex = vm.FilteredCommandHistory.IndexOf("git pull");
        Assert.True(pushIndex >= 0 && pullIndex >= 0);
        Assert.True(pushIndex < pullIndex, "Most recently executed command should rank before older command with identical score in command search.");
    }

    [Fact]
    public void DirectorySearch_EqualMatchScore_RanksMostRecentlyUsedFirst()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Two directory paths with identical score matching "folder"
        session.SimulateDirectoryChange(@"C:\test\folder1");
        session.SimulateDirectoryChange(@"C:\test\folder2");

        // Act: search for "folder"
        vm.DirectoryFilterQuery = "folder";

        // Assert: "folder2" visited second (newer), so it ranks before "folder1"
        Assert.True(vm.FilteredDirectoryHistory.Count >= 2);
        var dir2Norm = DirectoryHistoryService.NormalizePath(@"C:\test\folder2");
        var dir1Norm = DirectoryHistoryService.NormalizePath(@"C:\test\folder1");
        var index2 = vm.FilteredDirectoryHistory.IndexOf(dir2Norm);
        var index1 = vm.FilteredDirectoryHistory.IndexOf(dir1Norm);
        Assert.True(index2 >= 0 && index1 >= 0);
        Assert.True(index2 < index1, "Most recently visited directory should rank before older directory with identical score in directory search.");
    }

    [Fact]
    public void CommandSearch_PrefixMatchesDifferentLengths_RanksMostRecentlyUsedFirst()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        // Commands with different lengths: git log (7 chars), git commit -m "fix" (19 chars)
        session.SimulateCommandExecuted("git log");
        session.SimulateCommandExecuted("git commit -m \"fix\"");

        // Act: search for "git"
        vm.CommandFilterQuery = "git";

        // Assert: "git commit -m \"fix\"" is newer (executed second), so it should rank before "git log"
        Assert.True(vm.FilteredCommandHistory.Count >= 2);
        var commitIndex = vm.FilteredCommandHistory.IndexOf("git commit -m \"fix\"");
        var logIndex = vm.FilteredCommandHistory.IndexOf("git log");
        Assert.True(commitIndex >= 0 && logIndex >= 0);
        Assert.True(commitIndex < logIndex, "Newer command with same match quality (prefix) must rank before older shorter command.");
    }

    [Fact]
    public void DirectorySearch_DifferentLengths_RanksMostRecentlyUsedFirst()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);
        vm.StartSession();

        session.SimulateDirectoryChange(@"C:\projekte\short");
        session.SimulateDirectoryChange(@"C:\projekte\very\long\nested\directory\path");

        // Act: search for "projekte"
        vm.DirectoryFilterQuery = "projekte";

        // Assert: longer path visited second (newer), so it ranks before shorter older path
        Assert.True(vm.FilteredDirectoryHistory.Count >= 2);
        var shortNorm = DirectoryHistoryService.NormalizePath(@"C:\projekte\short");
        var longNorm = DirectoryHistoryService.NormalizePath(@"C:\projekte\very\long\nested\directory\path");
        var longIndex = vm.FilteredDirectoryHistory.IndexOf(longNorm);
        var shortIndex = vm.FilteredDirectoryHistory.IndexOf(shortNorm);
        Assert.True(longIndex >= 0 && shortIndex >= 0);
        Assert.True(longIndex < shortIndex, "Newer directory with same match quality must rank before older shorter directory.");
    }

    [Fact]
    public void Dispose_CleansUpTerminalModelUpdateUI_AndClearsHistoryCollections()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        var vm = new TerminalTabViewModel(session);
        vm.TerminalModel.UpdateUI = () => { };
        session.SimulateCommandExecuted("dir");
        session.SimulateDirectoryChange(@"C:\projekte\app");

        Assert.NotEmpty(vm.CommandHistory);
        Assert.NotEmpty(vm.DirectoryHistory);
        Assert.NotNull(vm.TerminalModel.UpdateUI);

        // Act
        vm.Dispose();

        // Assert
        Assert.Null(vm.TerminalModel.UpdateUI);
        Assert.Empty(vm.CommandHistory);
        Assert.Empty(vm.DirectoryHistory);
        Assert.Empty(vm.GlobalHistory);
    }

    [Fact]
    public void FlushPendingUiFeed_TrimsBufferCapacity_WhenExceedingLimit()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\projekte\app");
        using var vm = new TerminalTabViewModel(session);

        var largeChunk = new string('A', 70000);
        var bufferField = typeof(TerminalTabViewModel).GetField("_pendingUiFeedBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(bufferField);
        var sb = (StringBuilder)bufferField.GetValue(vm)!;

        // Simulate incoming chunk in the buffer
        lock (sb)
        {
            sb.Append(largeChunk);
        }

        Assert.True(sb.Capacity > 65536);

        // Act
        vm.FlushPendingUiFeed();

        // Assert
        Assert.Equal(0, sb.Length);
        Assert.True(sb.Capacity <= 4096, "Buffer capacity should be trimmed down to 4096 to prevent memory retention.");
    }

    [Fact]
    public void OnSessionDataReceived_WhenTabHasInitialPane_FeedsTerminalModelExactlyOnce()
    {
        // Arrange
        var session = new MockPowerShellSession("PowerShell", @"C:\test");
        using var vm = new TerminalTabViewModel(session);

        // Act - Simulate prompt output from shell
        var promptText = "PS C:\\test> ";
        session.SimulateDataReceived(Encoding.UTF8.GetBytes(promptText));

        // Assert - The text must appear exactly once, not duplicated
        var line = vm.TerminalModel.Terminal.Buffer.GetLine(0)?.TranslateToString(true) ?? string.Empty;
        Assert.Equal("PS C:\\test>", line);
    }

    [Fact]
    public void PruneDirectoryHistory_PrunesNonExistentDirectoriesFromSharedService()
    {
        // Arrange
        var dirService = new DirectoryHistoryService();
        var existingDir = AppContext.BaseDirectory;
        var fakeDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));

        dirService.RecordDirectory(existingDir);
        dirService.RecordDirectory(fakeDir);

        var session = new MockPowerShellSession("PowerShell", existingDir);
        using var vm = new TerminalTabViewModel(session, directoryHistoryService: dirService);

        Assert.Contains(DirectoryHistoryService.NormalizePath(existingDir), vm.DirectoryHistory);
        Assert.Contains(fakeDir, vm.DirectoryHistory);

        // Act
        vm.PruneDirectoryHistory();

        // Assert
        Assert.Contains(DirectoryHistoryService.NormalizePath(existingDir), vm.DirectoryHistory);
        Assert.DoesNotContain(fakeDir, vm.DirectoryHistory);
    }

    [Fact]
    public void SyncCollection_WhenSourceMatchesTarget_FiresZeroCollectionChangedEvents()
    {
        // Arrange
        var target = new ObservableCollection<string> { "item1", "item2", "item3" };
        var source = new List<string> { "item1", "item2", "item3" };
        var eventCount = 0;
        target.CollectionChanged += (_, _) => eventCount++;

        // Act
        TerminalTabViewModel.SyncCollection(target, source);

        // Assert: completely unchanged, zero events fired
        Assert.Equal(0, eventCount);
        Assert.Equal(source, target);
    }

    [Fact]
    public void SyncCollection_WhenSingleItemDiffers_FiresSingleReplaceEvent()
    {
        // Arrange
        var target = new ObservableCollection<string> { "a", "b", "c" };
        var source = new List<string> { "a", "modified", "c" };
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        TerminalTabViewModel.SyncCollection(target, source);

        // Assert
        Assert.Single(actions);
        Assert.Equal(NotifyCollectionChangedAction.Replace, actions[0]);
        Assert.Equal(new[] { "a", "modified", "c" }, target);
    }

    [Fact]
    public void SyncCollection_WhenSourceIsShorter_TrimsExcessItemsFromEnd()
    {
        // Arrange
        var target = new ObservableCollection<string> { "a", "b", "c", "d" };
        var source = new List<string> { "a", "b" };
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        TerminalTabViewModel.SyncCollection(target, source);

        // Assert
        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(NotifyCollectionChangedAction.Remove, a));
        Assert.Equal(new[] { "a", "b" }, target);
    }

    [Fact]
    public void SyncCollection_WhenSourceIsLonger_AppendsNewItems()
    {
        // Arrange
        var target = new ObservableCollection<string> { "a" };
        var source = new List<string> { "a", "b", "c" };
        var actions = new List<NotifyCollectionChangedAction>();
        target.CollectionChanged += (_, e) => actions.Add(e.Action);

        // Act
        TerminalTabViewModel.SyncCollection(target, source);

        // Assert
        Assert.Equal(2, actions.Count);
        Assert.All(actions, a => Assert.Equal(NotifyCollectionChangedAction.Add, a));
        Assert.Equal(new[] { "a", "b", "c" }, target);
    }
}
