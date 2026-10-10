using System;

namespace MultiShell.Services;

/// <summary>
/// Service providing desktop notifications for completed long-running commands (REQ-TERM-012).
/// </summary>
public interface IToastNotificationService
{
    /// <summary>
    /// Displays a native desktop notification when a long-running command completes in an inactive tab.
    /// </summary>
    /// <param name="tabTitle">The title of the tab hosting the command.</param>
    /// <param name="command">The command line string executed.</param>
    /// <param name="duration">Total execution duration.</param>
    /// <param name="exitCode">Exit code returned by the command or process.</param>
    /// <param name="onActivated">Callback invoked when the user clicks or activates the notification.</param>
    void ShowProcessCompletionNotification(
        string tabTitle,
        string command,
        TimeSpan duration,
        int exitCode,
        Action onActivated);
}
