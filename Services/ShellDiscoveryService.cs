using System;
using System.Collections.Generic;
using System.IO;

namespace MultiShell.Services;

/// <summary>
/// Discovers installed shells by checking PATH, system directories, and standard installation locations.
/// </summary>
public class ShellDiscoveryService : IShellDiscoveryService
{
    private readonly ILocalizationService? _localizationService;

    public ShellDiscoveryService(ILocalizationService? localizationService = null)
    {
        _localizationService = localizationService;
    }

    public IReadOnlyList<ShellOptionInfo> GetAvailableShells()
    {
        var shells = new List<ShellOptionInfo>();

        // 1. PowerShell (pwsh.exe or powershell.exe)
        var pwshPath = ResolveExecutable("pwsh.exe");
        var winPsPath = ResolveExecutable("powershell.exe");
        var psPath = pwshPath ?? winPsPath;
        var psName = pwshPath != null ? "PowerShell 7" : "Windows PowerShell";
        shells.Add(new ShellOptionInfo(
            ShellType.PowerShell,
            psName,
            "PS",
            psPath,
            psPath != null));

        // 2. NuShell (nu.exe)
        var nuPath = ResolveExecutable("nu.exe");
        shells.Add(new ShellOptionInfo(
            ShellType.NuShell,
            "NuShell",
            "NU",
            nuPath,
            nuPath != null));

        // 3. WSL (wsl.exe)
        var wslPath = ResolveExecutable("wsl.exe");
        shells.Add(new ShellOptionInfo(
            ShellType.WSL,
            "WSL (Linux)",
            "WSL",
            wslPath,
            wslPath != null));

        // 4. Command Prompt (cmd.exe)
        var cmdPath = ResolveExecutable("cmd.exe") ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
        shells.Add(new ShellOptionInfo(
            ShellType.CMD,
            _localizationService?["Shell_CMD"] ?? "Command Prompt",
            "CMD",
            cmdPath,
            File.Exists(cmdPath) || cmdPath != null));

        return shells;
    }

    public static string? ResolveExecutable(string name)
    {
        // 1. Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim('"', ' '), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }

        // 2. Standard Windows System paths
        var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);

        if (name.Equals("powershell.exe", StringComparison.OrdinalIgnoreCase))
        {
            var winPs = Path.Combine(winDir, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
            if (File.Exists(winPs)) return winPs;
        }

        if (name.Equals("cmd.exe", StringComparison.OrdinalIgnoreCase))
        {
            var cmd = Path.Combine(sysDir, "cmd.exe");
            if (File.Exists(cmd)) return cmd;
        }

        if (name.Equals("wsl.exe", StringComparison.OrdinalIgnoreCase))
        {
            var wsl = Path.Combine(sysDir, "wsl.exe");
            if (File.Exists(wsl)) return wsl;
        }

        // 3. Common user install locations (Scoop, WinGet, Cargo)
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (name.Equals("nu.exe", StringComparison.OrdinalIgnoreCase))
        {
            var cargoNu = Path.Combine(userProfile, ".cargo", "bin", "nu.exe");
            if (File.Exists(cargoNu)) return cargoNu;

            var scoopNu = Path.Combine(userProfile, "scoop", "shims", "nu.exe");
            if (File.Exists(scoopNu)) return scoopNu;
        }

        return null;
    }
}
