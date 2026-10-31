using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

public class ShellCommandFilterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsInternalConfigurationCommand_WithNullOrWhitespace_ReturnsTrue(string? command)
    {
        Assert.True(ShellCommandFilter.IsInternalConfigurationCommand(command));
    }

    [Theory]
    [InlineData("chcp 65001")]
    [InlineData("chcp")]
    [InlineData("[Console]::OutputEncoding = [System.Text.Encoding]::UTF8")]
    [InlineData("$OutputEncoding = [System.Text.Encoding]::UTF8")]
    [InlineData("$function:prompt = { 'PS > ' }")]
    [InlineData("Set-PSReadLineOption -HistorySaveStyle SaveNothing")]
    [InlineData("__multishell_init")]
    [InlineData("__multishell_hook_prompt")]
    public void IsInternalConfigurationCommand_WithInternalCommands_ReturnsTrue(string command)
    {
        Assert.True(ShellCommandFilter.IsInternalConfigurationCommand(command));
    }

    [Theory]
    [InlineData("git status")]
    [InlineData("git commit -m 'feat: message'")]
    [InlineData("dotnet test")]
    [InlineData("cargo build --release")]
    [InlineData("cd my-repo")]
    [InlineData("ls -la")]
    [InlineData("echo Hello World")]
    [InlineData("npm run build")]
    public void IsInternalConfigurationCommand_WithRegularUserCommands_ReturnsFalse(string command)
    {
        Assert.False(ShellCommandFilter.IsInternalConfigurationCommand(command));
    }

    [Theory]
    [InlineData("docker login --password mySecretPassword123", "docker login --password ******")]
    [InlineData("docker login --password=secretPass", "docker login --password=******")]
    [InlineData("curl -H 'Authorization: Bearer myTokenVal123' https://api.com", "curl -H 'Authorization: Bearer ******' https://api.com")]
    [InlineData("git clone https://john:superSecretToken@github.com/org/repo.git", "git clone https://john:******@github.com/org/repo.git")]
    [InlineData("app --token s3cr3tT0k3n --api-key 987654321", "app --token ****** --api-key ******")]
    public void SanitizeCommandForHistory_MasksSensitiveCredentials(string input, string expected)
    {
        var sanitized = ShellCommandFilter.SanitizeCommandForHistory(input);
        Assert.Equal(expected, sanitized);
    }

    [Fact]
    public void SanitizeCommandForHistory_LeavesSafeCommandsUntouched()
    {
        var safe = "git commit -m 'feat: add user authentication'";
        var sanitized = ShellCommandFilter.SanitizeCommandForHistory(safe);
        Assert.Equal(safe, sanitized);
    }
}
