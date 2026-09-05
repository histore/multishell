using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Implements persistent terminal profile management.
/// </summary>
public class TerminalProfileService : ITerminalProfileService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<TerminalProfile> _profiles = new();
    private readonly ILocalizationService? _localizationService;

    public event Action? ProfilesChanged;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public TerminalProfileService(string? customFilePath = null, ILocalizationService? localizationService = null)
    {
        _localizationService = localizationService;
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "MultiShell");
            _filePath = Path.Combine(appDir, "terminal_profiles.json");
        }

        LoadProfilesSynchronously();
    }

    private void LoadProfilesSynchronously()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var loaded = JsonSerializer.Deserialize(json, MultiShellJsonSerializerContext.Default.ListTerminalProfile);
                    if (loaded != null && loaded.Count > 0)
                    {
                        lock (_profiles)
                        {
                            _profiles.Clear();
                            _profiles.AddRange(loaded.Select(NormalizeProfile));
                        }
                        return;
                    }
                }
            }
        }
        catch { }

        var defaults = CreateDefaultProfiles();
        lock (_profiles)
        {
            _profiles.Clear();
            _profiles.AddRange(defaults);
        }
    }

    public IReadOnlyList<TerminalProfile> GetProfiles()
    {
        lock (_profiles)
        {
            return _profiles.ToList();
        }
    }

    public TerminalProfile? GetProfile(Guid id)
    {
        lock (_profiles)
        {
            return _profiles.FirstOrDefault(p => p.Id == id);
        }
    }

    public async Task AddProfileAsync(TerminalProfile profile)
    {
        var normalized = NormalizeProfile(profile);
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_profiles)
            {
                _profiles.Add(normalized);
            }
            await SaveInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public async Task UpdateProfileAsync(TerminalProfile profile)
    {
        var normalized = NormalizeProfile(profile);
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_profiles)
            {
                var index = _profiles.FindIndex(p => p.Id == normalized.Id);
                if (index >= 0)
                {
                    _profiles[index] = normalized;
                }
            }
            await SaveInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public async Task<bool> DeleteProfileAsync(Guid id)
    {
        bool removed = false;
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            lock (_profiles)
            {
                var index = _profiles.FindIndex(p => p.Id == id);
                if (index >= 0)
                {
                    _profiles.RemoveAt(index);
                    removed = true;
                }
            }
            if (removed)
            {
                await SaveInternalAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _semaphore.Release();
        }
        if (removed)
        {
            ProfilesChanged?.Invoke();
        }
        return removed;
    }

    public async Task ResetToDefaultsAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var defaults = CreateDefaultProfiles();
            lock (_profiles)
            {
                _profiles.Clear();
                _profiles.AddRange(defaults);
            }
            await SaveInternalAsync().ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public async Task LoadProfilesAsync()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(json))
            {
                var loaded = JsonSerializer.Deserialize(json, MultiShellJsonSerializerContext.Default.ListTerminalProfile);
                if (loaded != null && loaded.Count > 0)
                {
                    lock (_profiles)
                    {
                        _profiles.Clear();
                        _profiles.AddRange(loaded.Select(NormalizeProfile));
                    }
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public static string GetDefaultWorkingDirectory()
    {
        var userDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrWhiteSpace(userDir) ? userDir : Environment.CurrentDirectory;
    }

    private static TerminalProfile NormalizeProfile(TerminalProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.WorkingDirectory))
        {
            return profile with { WorkingDirectory = GetDefaultWorkingDirectory() };
        }
        return profile;
    }

    private List<TerminalProfile> CreateDefaultProfiles()
    {
        var list = new List<TerminalProfile>();
        var defaultDir = GetDefaultWorkingDirectory();

        // 1. PowerShell 7 / Windows PowerShell
        var pwshPath = ShellDiscoveryService.ResolveExecutable("pwsh.exe");
        var winPsPath = ShellDiscoveryService.ResolveExecutable("powershell.exe");
        var psPath = pwshPath ?? winPsPath ?? "powershell.exe";
        var psName = pwshPath != null ? "PowerShell 7" : "Windows PowerShell";
        list.Add(new TerminalProfile(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            psName,
            psPath,
            Arguments: "-NoLogo",
            WorkingDirectory: defaultDir,
            IconTag: "PS",
            ShellType: ShellType.PowerShell,
            IsBuiltIn: true));

        // 2. Command Prompt
        var cmdPath = ShellDiscoveryService.ResolveExecutable("cmd.exe") ?? "cmd.exe";
        list.Add(new TerminalProfile(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            _localizationService?["Shell_CMD"] ?? "Command Prompt",
            cmdPath,
            Arguments: null,
            WorkingDirectory: defaultDir,
            IconTag: "CMD",
            ShellType: ShellType.CMD,
            IsBuiltIn: true));

        // 3. WSL
        var wslPath = ShellDiscoveryService.ResolveExecutable("wsl.exe");
        if (wslPath != null)
        {
            list.Add(new TerminalProfile(
                Guid.Parse("33333333-3333-3333-3333-333333333333"),
                "WSL (Linux)",
                wslPath,
                Arguments: null,
                WorkingDirectory: defaultDir,
                IconTag: "WSL",
                ShellType: ShellType.WSL,
                IsBuiltIn: true));
        }

        // 4. NuShell (if found)
        var nuPath = ShellDiscoveryService.ResolveExecutable("nu.exe");
        if (nuPath != null)
        {
            list.Add(new TerminalProfile(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                "NuShell",
                nuPath,
                Arguments: null,
                WorkingDirectory: defaultDir,
                IconTag: "NU",
                ShellType: ShellType.NuShell,
                IsBuiltIn: true));
        }

        return list;
    }

    private async Task SaveInternalAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            List<TerminalProfile> copy;
            lock (_profiles)
            {
                copy = _profiles.ToList();
            }

            var json = JsonSerializer.Serialize(copy, MultiShellJsonSerializerContext.Default.ListTerminalProfile);
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save terminal profiles: {ex.Message}");
        }
    }
}
