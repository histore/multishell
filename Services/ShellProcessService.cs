namespace MultiShell.Services;

public class ShellProcessService : IShellProcessService
{
    public IShellSession CreateSession(
        string title,
        string? workingDirectory = null,
        ShellType shellType = ShellType.PowerShell,
        string? customExecutable = null,
        string? customArguments = null)
    {
        return new ShellSession(title, workingDirectory, shellType, customExecutable, customArguments);
    }
}
