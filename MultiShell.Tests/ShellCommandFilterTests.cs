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
}
