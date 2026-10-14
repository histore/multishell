using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Platform-native desktop notification service for Windows (REQ-TERM-012).
/// Dispatches toast notifications for long-running processes via Windows notification APIs.
/// </summary>
public sealed class WindowsToastNotificationService : IToastNotificationService
{
    private readonly ILocalizationService? _localizationService;

    public WindowsToastNotificationService(ILocalizationService? localizationService = null)
    {
        _localizationService = localizationService;
    }

    /// <inheritdoc />
    public void ShowProcessCompletionNotification(
        string tabTitle,
        string command,
        TimeSpan duration,
        int exitCode,
        Action onActivated)
    {
        Task.Run(() =>
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    ShowWindowsToast(tabTitle, command, duration, exitCode);
                }
            }
            catch
            {
                // Non-critical: failure to display a desktop toast should never crash the terminal
            }
        });
    }

    private void ShowWindowsToast(string tabTitle, string command, TimeSpan duration, int exitCode)
    {
        var successLabel = _localizationService?["Process_Activity_Success"] ?? "Success";
        var failedLabel = _localizationService?["Process_Activity_Failed"] ?? "Failed";
        var status = exitCode == 0 ? successLabel : $"{failedLabel} (Exit-Code {exitCode})";
        var safeCmd = (command.Length > 60 ? command[..57] + "..." : command).Replace("\"", "`\"");
        var safeTitle = tabTitle.Replace("\"", "`\"");
        var durationText = duration.TotalSeconds >= 60
            ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
            : $"{duration.TotalSeconds:0.1}s";

        var script = $$"""
            try {
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
                $textNodes = $template.GetElementsByTagName('text')
                $textNodes.Item(0).InnerText = 'MultiShell: {{safeTitle}} ({{status}})'
                $textNodes.Item(1).InnerText = '`"{{safeCmd}}`" ({{durationText}})'
                $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('MultiShell').Show($toast)
            } catch { }
            """;

        var encoded = Convert.ToBase64String(System.Text.Encoding.Unicode.GetBytes(script));
        var psi = new ProcessStartInfo("powershell.exe", $"-NoProfile -NonInteractive -EncodedCommand {encoded}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        using var proc = Process.Start(psi);
    }
}
