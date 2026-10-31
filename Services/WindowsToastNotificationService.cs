using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Platform-native desktop notification service for Windows (REQ-TERM-012).
/// Dispatches toast notifications for long-running processes via Windows notification APIs.
/// </summary>
public sealed class WindowsToastNotificationService : IToastNotificationService
{
    private readonly ILocalizationService? _localizationService;

    internal Func<ProcessStartInfo, Process?>? ProcessStarter { get; set; }

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

    internal void ShowWindowsToast(string tabTitle, string command, TimeSpan duration, int exitCode)
    {
        var successLabel = _localizationService?["Process_Activity_Success"] ?? "Success";
        var failedLabel = _localizationService?["Process_Activity_Failed"] ?? "Failed";

        var (encodedScript, powerShellPath) = BuildToastInvocation(tabTitle, command, duration, exitCode, successLabel, failedLabel);

        var psi = new ProcessStartInfo(powerShellPath, $"-NoProfile -NonInteractive -EncodedCommand {encodedScript}")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        if (ProcessStarter != null)
        {
            using var customProc = ProcessStarter(psi);
            return;
        }

        using var proc = Process.Start(psi);
    }

    internal static (string EncodedScript, string PowerShellPath) BuildToastInvocation(
        string tabTitle,
        string command,
        TimeSpan duration,
        int exitCode,
        string successLabel = "Success",
        string failedLabel = "Failed")
    {
        var status = exitCode == 0 ? successLabel : $"{failedLabel} (Exit-Code {exitCode})";
        var truncatedCmd = command.Length > 60 ? command[..57] + "..." : command;
        var durationText = duration.TotalSeconds >= 60
            ? $"{(int)duration.TotalMinutes}m {duration.Seconds}s"
            : $"{duration.TotalSeconds:0.1}s";

        var titleText = $"MultiShell: {tabTitle} ({status})";
        var bodyText = $"\"{truncatedCmd}\" ({durationText})";

        // Encode title and body as UTF-8 Base64 strings to guarantee immune encapsulation
        // against arbitrary quote breakout, PowerShell injection (CWE-94 / CWE-88), or special characters.
        var titleBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(titleText));
        var bodyBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(bodyText));

        var script = $$"""
            try {
                [Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
                $title = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{{titleBase64}}'))
                $body = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String('{{bodyBase64}}'))
                $template = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
                $textNodes = $template.GetElementsByTagName('text')
                $textNodes.Item(0).InnerText = $title
                $textNodes.Item(1).InnerText = $body
                $toast = [Windows.UI.Notifications.ToastNotification]::new($template)
                [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('MultiShell').Show($toast)
            } catch { }
            """;

        var encodedScript = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));

        var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
        var winPowerShell = Path.Combine(sysDir, "WindowsPowerShell", "v1.0", "powershell.exe");
        var powerShellPath = File.Exists(winPowerShell) ? winPowerShell : "powershell.exe";

        return (encodedScript, powerShellPath);
    }
}
