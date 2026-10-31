using System;
using System.Diagnostics;
using System.Text;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class WindowsToastNotificationServiceTests
{
    [Fact]
    public void BuildToastInvocation_UsesFullSystemPathForPowerShell_WhenAvailable()
    {
        // Act
        var (_, powerShellPath) = WindowsToastNotificationService.BuildToastInvocation(
            "Terminal Tab",
            "dotnet test",
            TimeSpan.FromSeconds(5),
            exitCode: 0);

        // Assert
        if (OperatingSystem.IsWindows())
        {
            Assert.Contains("System32", powerShellPath, StringComparison.OrdinalIgnoreCase);
            Assert.EndsWith("powershell.exe", powerShellPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Theory]
    [InlineData("test' ; Start-Process calc.exe ; '")]
    [InlineData("test\" ; Remove-Item C:\\ ; \"")]
    [InlineData("git commit -m 'feat: add $variable `\"nested`\"'")]
    [InlineData("python -c \"import os; os.system('whoami')\"")]
    [InlineData("echo $env:PATH && `whoami` { scriptblock }")]
    public void BuildToastInvocation_SafelyEncapsulatesMaliciousAndSpecialCharacters_WithoutScriptBreakout(string maliciousInput)
    {
        // Act
        var (encodedScript, _) = WindowsToastNotificationService.BuildToastInvocation(
            tabTitle: maliciousInput,
            command: maliciousInput,
            duration: TimeSpan.FromSeconds(2.5),
            exitCode: 1,
            successLabel: "Erfolg",
            failedLabel: "Fehlgeschlagen");

        // Decode the inner powershell script
        var rawScriptBytes = Convert.FromBase64String(encodedScript);
        var scriptText = Encoding.Unicode.GetString(rawScriptBytes);

        // Assert that the raw script contains only Base64 literals for title and body,
        // and does NOT inject the malicious string raw into PowerShell script structure.
        Assert.DoesNotContain(maliciousInput, scriptText, StringComparison.Ordinal);
        Assert.Contains("FromBase64String('", scriptText, StringComparison.Ordinal);
        Assert.Contains("[System.Text.Encoding]::UTF8.GetString", scriptText, StringComparison.Ordinal);
    }

    [Fact]
    public void ShowWindowsToast_InvokesProcessStarterWithCorrectArguments()
    {
        // Arrange
        var service = new WindowsToastNotificationService();
        ProcessStartInfo? capturedPsi = null;

        service.ProcessStarter = psi =>
        {
            capturedPsi = psi;
            return null;
        };

        // Act
        service.ShowWindowsToast("Tab 1", "echo 'hello world'", TimeSpan.FromSeconds(12), exitCode: 0);

        // Assert
        Assert.NotNull(capturedPsi);
        Assert.Contains("-NoProfile -NonInteractive -EncodedCommand", capturedPsi.Arguments);
        Assert.False(capturedPsi.UseShellExecute);
        Assert.True(capturedPsi.CreateNoWindow);
    }
}
