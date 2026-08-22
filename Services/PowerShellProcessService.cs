namespace MultiShell.Services;

/// <summary>
/// Default implementation of IPowerShellProcessService producing PowerShellSession instances.
/// </summary>
public class PowerShellProcessService : IPowerShellProcessService
{
    public IPowerShellSession CreateSession(string title, string? workingDirectory = null)
    {
        return new PowerShellSession(title, workingDirectory);
    }
}
