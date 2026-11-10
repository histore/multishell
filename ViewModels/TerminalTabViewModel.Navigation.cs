using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.Input;
using MultiShell.Models;
using MultiShell.Services;

namespace MultiShell.ViewModels;

/// <summary>
/// Partial class managing terminal buffer scrolling/clearing and status bar path breadcrumb navigation.
/// </summary>
public partial class TerminalTabViewModel
{
    /// <summary>
    /// Opens the platform-specific native file browser in this tab's current working directory.
    /// </summary>
    [RelayCommand]
    public void OpenInFileBrowser()
    {
        FileBrowserHelper.OpenInFileBrowser(WorkingDirectory);
    }

    /// <summary>
    /// Scrolls the terminal scrollback buffer up by one page (REQ-TERM-003).
    /// </summary>
    public void PageUp() => TerminalModel.PageUp();

    /// <summary>
    /// Scrolls the terminal scrollback buffer down by one page (REQ-TERM-003).
    /// </summary>
    public void PageDown() => TerminalModel.PageDown();

    /// <summary>
    /// Sends the VT escape sequence for PageUp to the active PTY shell session (REQ-TERM-003).
    /// Plain PageUp: \x1b[5~, Alt+PageUp: \x1b[5;3~
    /// </summary>
    public void SendPageUp(bool alt = false)
    {
        SendInput(alt ? "\u001b[5;3~"u8.ToArray() : "\u001b[5~"u8.ToArray());
    }

    /// <summary>
    /// Sends the VT escape sequence for PageDown to the active PTY shell session (REQ-TERM-003).
    /// Plain PageDown: \x1b[6~, Alt+PageDown: \x1b[6;3~
    /// </summary>
    public void SendPageDown(bool alt = false)
    {
        SendInput(alt ? "\u001b[6;3~"u8.ToArray() : "\u001b[6~"u8.ToArray());
    }

    /// <summary>
    /// Clears the terminal screen and scrollback buffer (REQ-TERM-003).
    /// </summary>
    public void ClearBuffer()
    {
        // ANSI sequence: \x1b[2J (erase screen), \x1b[3J (erase scrollback), \x1b[H (cursor home)
        const string clearSequence = "\x1b[2J\x1b[3J\x1b[H";
        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            TerminalModel.Feed(clearSequence);
            TerminalModel.ClearSelection();
        }
        else
        {
            Dispatcher.UIThread.Post(() =>
            {
                TerminalModel.Feed(clearSequence);
                TerminalModel.ClearSelection();
            });
        }
    }

    private void OnLocalizationPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        UpdateSearchMatchSummary();
        OnPropertyChanged(nameof(ExecutionStatusTooltip));
        OnPropertyChanged(nameof(ProcessActivityText));
        UpdateBreadcrumbs();
    }

    /// <summary>
    /// Regenerates path breadcrumb segments with localized tooltips and interactive state
    /// based on current draft command state and process execution state (REQ-TERM-013).
    /// </summary>
    public void UpdateBreadcrumbs()
    {
        void DoUpdate()
        {
            var rawSegments = PathBreadcrumbParser.Parse(WorkingDirectory);
            if (rawSegments.Count == 0)
            {
                PathBreadcrumbs = Array.Empty<PathBreadcrumbSegment>();
                return;
            }

            var localizedSegments = new List<PathBreadcrumbSegment>(rawSegments.Count);
            foreach (var s in rawSegments)
            {
                bool isEnabled = true;
                string tooltip;

                if (s.IsCurrent)
                {
                    tooltip = (IsProcessRunning || HasDraftCommand)
                        ? Loc["Status_Breadcrumb_PickAndInsert_Tooltip"]
                        : Loc["Status_Breadcrumb_PickAndNavigate_Tooltip"];
                }
                else if (IsProcessRunning)
                {
                    isEnabled = false;
                    tooltip = Loc["Status_Breadcrumb_Disabled_ProcessRunning_Tooltip"];
                }
                else
                {
                    tooltip = HasDraftCommand
                        ? string.Format(Loc["Status_Breadcrumb_Insert_Tooltip"], s.FullPath)
                        : string.Format(Loc["Status_Breadcrumb_Navigate_Tooltip"], s.FullPath);
                }

                localizedSegments.Add(new PathBreadcrumbSegment(s.Name, s.FullPath, s.IsCurrent, tooltip, isEnabled));
            }

            PathBreadcrumbs = localizedSegments;
        }

        if (Application.Current == null || Dispatcher.UIThread.CheckAccess())
        {
            DoUpdate();
        }
        else
        {
            Dispatcher.UIThread.Post(DoUpdate);
        }
    }

    /// <summary>
    /// Handles user interaction when clicking a path breadcrumb in the status bar (REQ-TERM-013).
    /// </summary>
    [RelayCommand]
    public async Task HandleBreadcrumbClickAsync(PathBreadcrumbSegment? segment)
    {
        if (segment == null) return;

        if (segment.IsCurrent)
        {
            if (RequestFolderPickerAsync == null) return;

            var selectedFolder = await RequestFolderPickerAsync(segment.FullPath);
            if (string.IsNullOrWhiteSpace(selectedFolder)) return;

            if (IsProcessRunning || HasDraftCommand)
            {
                InsertPath(selectedFolder);
            }
            else
            {
                NavigateToDirectory(selectedFolder);
            }
            return;
        }

        if (IsProcessRunning || !segment.IsEnabled)
        {
            return;
        }

        if (HasDraftCommand)
        {
            InsertPath(segment.FullPath);
        }
        else
        {
            NavigateToDirectory(segment.FullPath);
        }
    }
}
