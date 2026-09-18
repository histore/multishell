using System;
using System.IO;
using System.Linq;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class DirectoryHistoryServiceTests
{
    [Fact]
    public void RecordDirectory_AddsNewDirectory()
    {
        // Arrange
        var service = new DirectoryHistoryService();

        // Act
        service.RecordDirectory(@"C:\Projects\Repo1");

        // Assert
        var history = service.GetHistory();
        Assert.Single(history);
        Assert.Equal(@"C:\Projects\Repo1", history[0]);
    }

    [Fact]
    public void RecordDirectory_DeduplicatesAndMovesToNewestPosition_MRU()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        service.RecordDirectory(@"C:\Projects\Repo1");
        service.RecordDirectory(@"C:\Projects\Repo2");
        service.RecordDirectory(@"C:\Projects\Repo3");

        // Act - Re-visit Repo1 (should be moved from first position to the newest last position)
        service.RecordDirectory(@"C:\Projects\Repo1");

        // Assert
        var history = service.GetHistory();
        Assert.Equal(3, history.Count);
        Assert.Equal(@"C:\Projects\Repo2", history[0]);
        Assert.Equal(@"C:\Projects\Repo3", history[1]);
        Assert.Equal(@"C:\Projects\Repo1", history[2]);
    }

    [Fact]
    public void RecordDirectory_NormalizesSeparatorsAndIgnoresCaseForDeduplication()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        service.RecordDirectory(@"C:\Projects\Repo1");

        // Act - Re-visit with trailing slash and different casing
        service.RecordDirectory(@"c:\projects\repo1\");

        // Assert - Only one entry should remain, placed at newest position
        var history = service.GetHistory();
        Assert.Single(history);
        Assert.Equal(@"C:\Projects\Repo1", history[0], ignoreCase: true);
    }

    [Fact]
    public void RecordDirectory_CapsAt100Entries_EvictsOldest_FIFO()
    {
        // Arrange
        var service = new DirectoryHistoryService();

        // Act - Record 105 distinct directories
        for (int i = 1; i <= 105; i++)
        {
            service.RecordDirectory($@"C:\Projects\Repo_{i:D3}");
        }

        // Assert - Exactly 100 entries must remain, oldest (Repo_001..Repo_005) evicted
        var history = service.GetHistory();
        Assert.Equal(100, history.Count);
        Assert.Equal(@"C:\Projects\Repo_006", history[0]);
        Assert.Equal(@"C:\Projects\Repo_105", history[^1]);
    }

    [Fact]
    public void RecordDirectory_FiresHistoryChangedEvent()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        bool eventFired = false;
        service.HistoryChanged += () => eventFired = true;

        // Act
        service.RecordDirectory(@"C:\Projects\Repo1");

        // Assert
        Assert.True(eventFired);
    }

    [Fact]
    public void RecordDirectory_IgnoresNullOrEmpty()
    {
        // Arrange
        var service = new DirectoryHistoryService();

        // Act
        service.RecordDirectory(null);
        service.RecordDirectory(string.Empty);
        service.RecordDirectory("   ");

        // Assert
        Assert.Empty(service.GetHistory());
    }

    [Fact]
    public void ImportAll_PreservesOrderAndDeduplicatesAndCapsAt100()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        var paths = Enumerable.Range(1, 110).Select(i => $@"C:\Folder_{i:D3}").ToList();
        // Add duplicate of folder 10 at the end
        paths.Add(@"C:\Folder_010");

        // Act
        service.ImportAll(paths);

        // Assert
        var history = service.GetHistory();
        Assert.Equal(100, history.Count);
        // Folder 010 should be at the very end as newest
        Assert.Equal(@"C:\Folder_010", history[^1]);
    }

    [Fact]
    public void ExportAll_ReturnsAccurateCopy()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        service.RecordDirectory(@"C:\Projects\A");
        service.RecordDirectory(@"C:\Projects\B");

        // Act
        var exported = service.ExportAll();

        // Assert
        Assert.Equal(2, exported.Count);
        Assert.Equal(new[] { @"C:\Projects\A", @"C:\Projects\B" }, exported);
    }

    [Fact]
    public void Clear_EmptiesListAndFiresEvent()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        service.RecordDirectory(@"C:\Projects\A");
        bool eventFired = false;
        service.HistoryChanged += () => eventFired = true;

        // Act
        service.Clear();

        // Assert
        Assert.Empty(service.GetHistory());
        Assert.True(eventFired);
    }

    [Fact]
    public void PruneNonExistentDirectories_RemovesDeletedDirectoriesAndFiresEvent()
    {
        // Arrange
        var service = new DirectoryHistoryService();
        var existingDir = AppContext.BaseDirectory; // Guaranteed to exist
        var fakeDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

        service.RecordDirectory(existingDir);
        service.RecordDirectory(fakeDir);

        bool eventFired = false;
        service.HistoryChanged += () => eventFired = true;

        // Act
        service.PruneNonExistentDirectories();

        // Assert
        var history = service.GetHistory();
        Assert.Single(history);
        Assert.Equal(DirectoryHistoryService.NormalizePath(existingDir), history[0]);
        Assert.True(eventFired);
    }
}
