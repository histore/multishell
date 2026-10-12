using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using MultiShell.Models;
using MultiShell.Services;
using MultiShell.ViewModels;
using Xunit;

namespace MultiShell.Tests;

public class StatusBreadcrumbNavigationTests
{
    private class MockShellSession : IShellSession
    {
        public Guid SessionId { get; } = Guid.NewGuid();
        public string Title { get; }
        public string? WorkingDirectory { get; set; }
        public ShellType ShellType { get; set; } = ShellType.PowerShell;
        public bool IsRunning { get; set; } = true;

        public List<string> SentStrings { get; } = new();

        public event Action<byte[]>? DataReceived;
        public event Action<int>? Exited;
        public event Action<string>? WorkingDirectoryChanged;
        public event Action<string>? CommandExecuted { add { } remove { } }
        public event Action<int>? CommandCompleted { add { } remove { } }

        public MockShellSession(string title, string? workingDir = null)
        {
            Title = title;
            WorkingDirectory = workingDir;
        }

        public void Start() => IsRunning = true;

        public void Send(byte[] input)
        {
            SentStrings.Add(Encoding.UTF8.GetString(input));
        }

        public void Resize(int cols, int rows) { }
        public void Dispose() { }

        public void SimulateDataReceived(byte[] data) => DataReceived?.Invoke(data);
        public void SimulateExit(int code) => Exited?.Invoke(code);
        public void SimulateWorkingDirectoryChanged(string newDir) => WorkingDirectoryChanged?.Invoke(newDir);
    }

    [Fact]
    public void PathBreadcrumbParser_ParsesWindowsRootCorrectly()
    {
        var segments = PathBreadcrumbParser.Parse(@"C:\");

        Assert.Single(segments);
        Assert.Equal("C:", segments[0].Name);
        Assert.Equal(@"C:\", segments[0].FullPath);
        Assert.True(segments[0].IsCurrent);
    }

    [Fact]
    public void PathBreadcrumbParser_ParsesWindowsNestedPathCorrectly()
    {
        var segments = PathBreadcrumbParser.Parse(@"C:\projekte\csharp\multishell");

        Assert.Equal(4, segments.Count);

        Assert.Equal("C:", segments[0].Name);
        Assert.Equal(@"C:\", segments[0].FullPath);
        Assert.False(segments[0].IsCurrent);

        Assert.Equal("projekte", segments[1].Name);
        Assert.Equal(@"C:\projekte", segments[1].FullPath);
        Assert.False(segments[1].IsCurrent);

        Assert.Equal("csharp", segments[2].Name);
        Assert.Equal(@"C:\projekte\csharp", segments[2].FullPath);
        Assert.False(segments[2].IsCurrent);

        Assert.Equal("multishell", segments[3].Name);
        Assert.Equal(@"C:\projekte\csharp\multishell", segments[3].FullPath);
        Assert.True(segments[3].IsCurrent);
    }

    [Fact]
    public void PathBreadcrumbParser_ParsesUnixRootAndNestedPathCorrectly()
    {
        var rootSegments = PathBreadcrumbParser.Parse("/");
        Assert.Single(rootSegments);
        Assert.Equal("/", rootSegments[0].Name);
        Assert.Equal("/", rootSegments[0].FullPath);
        Assert.True(rootSegments[0].IsCurrent);

        var nestedSegments = PathBreadcrumbParser.Parse("/home/user/project");
        Assert.Equal(4, nestedSegments.Count);
        Assert.Equal("/", nestedSegments[0].Name);
        Assert.Equal("/", nestedSegments[0].FullPath);
        Assert.False(nestedSegments[0].IsCurrent);

        Assert.Equal("home", nestedSegments[1].Name);
        Assert.Equal("/home", nestedSegments[1].FullPath);
        Assert.False(nestedSegments[1].IsCurrent);

        Assert.Equal("user", nestedSegments[2].Name);
        Assert.Equal("/home/user", nestedSegments[2].FullPath);
        Assert.False(nestedSegments[2].IsCurrent);

        Assert.Equal("project", nestedSegments[3].Name);
        Assert.Equal("/home/user/project", nestedSegments[3].FullPath);
        Assert.True(nestedSegments[3].IsCurrent);
    }

    [Fact]
    public void PathBreadcrumbParser_ParsesUncPathCorrectly()
    {
        var segments = PathBreadcrumbParser.Parse(@"\\nas\share\documents");

        Assert.Equal(2, segments.Count);
        Assert.Equal(@"\\nas\share", segments[0].Name);
        Assert.Equal(@"\\nas\share", segments[0].FullPath);
        Assert.False(segments[0].IsCurrent);

        Assert.Equal("documents", segments[1].Name);
        Assert.Equal(@"\\nas\share\documents", segments[1].FullPath);
        Assert.True(segments[1].IsCurrent);
    }

    [Fact]
    public void PathBreadcrumbParser_HandlesNullAndEmptyPaths()
    {
        Assert.Empty(PathBreadcrumbParser.Parse(null));
        Assert.Empty(PathBreadcrumbParser.Parse(""));
        Assert.Empty(PathBreadcrumbParser.Parse("   "));
    }

    [Fact]
    public void HasDraftCommand_TracksInputBufferAccurately()
    {
        var session = new MockShellSession("TestTab");
        var tabVm = new TerminalTabViewModel(session);

        Assert.False(tabVm.HasDraftCommand);

        // Typing characters
        tabVm.SendInput(Encoding.UTF8.GetBytes("git status"));
        Assert.True(tabVm.HasDraftCommand);

        // Backspace down to empty
        for (int i = 0; i < 10; i++)
        {
            tabVm.SendInput(new byte[] { 0x08 }); // backspace
        }
        Assert.False(tabVm.HasDraftCommand);

        // Re-type and press Enter
        tabVm.SendInput(Encoding.UTF8.GetBytes("dir"));
        Assert.True(tabVm.HasDraftCommand);
        tabVm.SendInput(new byte[] { (byte)'\r' });
        Assert.False(tabVm.HasDraftCommand);

        // Type and cancel via Ctrl+C
        tabVm.SendInput(Encoding.UTF8.GetBytes("ping"));
        Assert.True(tabVm.HasDraftCommand);
        tabVm.SendInput(new byte[] { 0x03 }); // Ctrl+C
        Assert.False(tabVm.HasDraftCommand);
    }

    [Fact]
    public void UpdateBreadcrumbs_GeneratesModeAppropriateTooltips()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("de", isUserSelection: true);

        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session, localizationService: loc);

        // Initial state: HasDraftCommand == false
        Assert.False(tabVm.HasDraftCommand);
        Assert.Equal(3, tabVm.PathBreadcrumbs.Count);

        var parentSeg = tabVm.PathBreadcrumbs[1]; // C:\projekte
        var currentSeg = tabVm.PathBreadcrumbs[2]; // C:\projekte\multishell

        Assert.Contains("Zum Ordner wechseln: C:\\projekte", parentSeg.Tooltip);
        Assert.Contains("Ordner auswählen und dorthin wechseln...", currentSeg.Tooltip);

        // Type draft command
        tabVm.SendInput(Encoding.UTF8.GetBytes("git add "));
        Assert.True(tabVm.HasDraftCommand);

        parentSeg = tabVm.PathBreadcrumbs[1];
        currentSeg = tabVm.PathBreadcrumbs[2];

        Assert.Contains("Pfad einfügen: C:\\projekte", parentSeg.Tooltip);
        Assert.Contains("Ordner auswählen und Pfad einfügen...", currentSeg.Tooltip);
    }

    [Fact]
    public async Task HandleBreadcrumbClick_WithoutDraftCommand_ParentNavigatesDirectory()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\csharp\multishell");
        var tabVm = new TerminalTabViewModel(session);

        Assert.False(tabVm.HasDraftCommand);
        var parentSeg = tabVm.PathBreadcrumbs[1]; // C:\projekte

        await tabVm.HandleBreadcrumbClickAsync(parentSeg);

        // Should execute directory navigation command (Set-Location)
        Assert.Contains(session.SentStrings, s => s.Contains("Set-Location") && s.Contains(@"C:\projekte"));
    }

    [Fact]
    public async Task HandleBreadcrumbClick_WithDraftCommand_ParentInsertsPathWithoutNavigation()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\csharp\multishell");
        var tabVm = new TerminalTabViewModel(session);

        // Enter draft command
        tabVm.SendInput(Encoding.UTF8.GetBytes("git clone "));
        Assert.True(tabVm.HasDraftCommand);
        session.SentStrings.Clear();

        var parentSeg = tabVm.PathBreadcrumbs[1]; // C:\projekte

        await tabVm.HandleBreadcrumbClickAsync(parentSeg);

        // Should append path to prompt, not Set-Location
        Assert.DoesNotContain(session.SentStrings, s => s.Contains("Set-Location"));
        Assert.Contains(session.SentStrings, s => s.Contains(@"C:\projekte"));
    }

    [Fact]
    public async Task HandleBreadcrumbClick_WithoutDraftCommand_CurrentFolderNavigatesToPickedFolder()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session);

        string? requestedStartDir = null;
        tabVm.RequestFolderPickerAsync = dir =>
        {
            requestedStartDir = dir;
            return Task.FromResult<string?>(@"C:\repos\target");
        };

        var currentSeg = tabVm.PathBreadcrumbs[2]; // multishell
        await tabVm.HandleBreadcrumbClickAsync(currentSeg);

        Assert.Equal(@"C:\projekte\multishell", requestedStartDir);
        // Navigates to picked folder
        Assert.Contains(session.SentStrings, s => s.Contains("Set-Location") && s.Contains(@"C:\repos\target"));
    }

    [Fact]
    public async Task HandleBreadcrumbClick_WithDraftCommand_CurrentFolderInsertsPickedPath()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session);

        // Enter draft command
        tabVm.SendInput(Encoding.UTF8.GetBytes("cd "));
        Assert.True(tabVm.HasDraftCommand);
        session.SentStrings.Clear();

        tabVm.RequestFolderPickerAsync = dir => Task.FromResult<string?>(@"C:\path with spaces");

        var currentSeg = tabVm.PathBreadcrumbs[2]; // multishell
        await tabVm.HandleBreadcrumbClickAsync(currentSeg);

        // Inserts quoted path, does NOT invoke Set-Location
        Assert.DoesNotContain(session.SentStrings, s => s.Contains("Set-Location"));
        Assert.Contains(session.SentStrings, s => s.Contains("\"C:\\path with spaces\""));
    }

    [Fact]
    public async Task HandleBreadcrumbClick_CurrentFolderCanceledPicker_DoesNothing()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session);
        session.SentStrings.Clear();

        tabVm.RequestFolderPickerAsync = dir => Task.FromResult<string?>(null);

        var currentSeg = tabVm.PathBreadcrumbs[2];
        await tabVm.HandleBreadcrumbClickAsync(currentSeg);

        Assert.Empty(session.SentStrings);
    }

    [Fact]
    public void UpdateBreadcrumbs_WhenProcessRunning_DisablesParentBreadcrumbsAndShowsProcessTooltip()
    {
        var loc = new LocalizationService();
        loc.SetLanguage("de", isUserSelection: true);

        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session, localizationService: loc);

        // Before process starts: all segments enabled
        Assert.All(tabVm.PathBreadcrumbs, seg => Assert.True(seg.IsEnabled));

        // Start command / process
        tabVm.MarkCommandStarted("ping 127.0.0.1");
        Assert.True(tabVm.IsProcessRunning);

        // Verify parent segments are disabled with process running tooltip
        var driveSeg = tabVm.PathBreadcrumbs[0]; // C:\
        var parentSeg = tabVm.PathBreadcrumbs[1]; // C:\projekte
        var currentSeg = tabVm.PathBreadcrumbs[2]; // multishell

        Assert.False(driveSeg.IsEnabled);
        Assert.Contains("Navigation während eines aktiven Prozesses nicht verfügbar", driveSeg.Tooltip);

        Assert.False(parentSeg.IsEnabled);
        Assert.Contains("Navigation während eines aktiven Prozesses nicht verfügbar", parentSeg.Tooltip);

        // Current directory segment remains enabled with pick-and-insert tooltip
        Assert.True(currentSeg.IsEnabled);
        Assert.Contains("Ordner auswählen und Pfad einfügen...", currentSeg.Tooltip);
    }

    [Fact]
    public async Task HandleBreadcrumbClick_WhenProcessRunning_ParentBreadcrumbDoesNotNavigateOrInsert()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\csharp\multishell");
        var tabVm = new TerminalTabViewModel(session);

        tabVm.MarkCommandStarted("npm run dev");
        session.SentStrings.Clear();

        var parentSeg = tabVm.PathBreadcrumbs[1]; // C:\projekte
        Assert.False(parentSeg.IsEnabled);

        await tabVm.HandleBreadcrumbClickAsync(parentSeg);

        // Should not send anything to session (no cd, no path insertion)
        Assert.Empty(session.SentStrings);
    }

    [Fact]
    public async Task HandleBreadcrumbClick_WhenProcessRunning_CurrentFolderOpensPickerAndInsertsPathWithoutDraftCommand()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session);

        // Process running, no draft command
        tabVm.MarkCommandStarted("python train.py");
        Assert.False(tabVm.HasDraftCommand);
        session.SentStrings.Clear();

        string? requestedStartDir = null;
        tabVm.RequestFolderPickerAsync = dir =>
        {
            requestedStartDir = dir;
            return Task.FromResult<string?>(@"C:\datasets\images");
        };

        var currentSeg = tabVm.PathBreadcrumbs[2];
        Assert.True(currentSeg.IsEnabled);

        await tabVm.HandleBreadcrumbClickAsync(currentSeg);

        // Picker was opened at current folder
        Assert.Equal(@"C:\projekte\multishell", requestedStartDir);

        // Inserts target path into prompt/input without cd/Set-Location navigation command
        Assert.DoesNotContain(session.SentStrings, s => s.Contains("Set-Location"));
        Assert.Contains(session.SentStrings, s => s.Contains(@"C:\datasets\images"));
    }

    [Fact]
    public void ExecutionStateChanged_UpdatesBreadcrumbStateDynamically()
    {
        var session = new MockShellSession("TestTab", @"C:\projekte\multishell");
        var tabVm = new TerminalTabViewModel(session);

        // Initial: Idle -> Parents enabled
        Assert.True(tabVm.PathBreadcrumbs[1].IsEnabled);

        // Running -> Parents disabled
        tabVm.MarkCommandStarted("dotnet build");
        Assert.False(tabVm.PathBreadcrumbs[1].IsEnabled);

        // Completed -> Parents re-enabled
        tabVm.OnSessionCommandCompleted(0);
        Assert.True(tabVm.PathBreadcrumbs[1].IsEnabled);
    }
}

