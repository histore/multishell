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
    private readonly Lock _profilesLock = new();
    private readonly ILocalizationService? _localizationService;

    public event Action? ProfilesChanged;

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
                if (TryApplyLoadedProfiles(json))
                {
                    return;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            System.Diagnostics.Debug.WriteLine($"[TerminalProfileService.LoadProfilesSynchronously] Failed to load profiles from '{_filePath}': {ex.Message}");
        }

        var defaults = CreateDefaultProfiles();
        ReplaceProfiles(defaults);
    }

    private bool TryApplyLoadedProfiles(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;

        var loaded = JsonSerializer.Deserialize(json, MultiShellJsonSerializerContext.Default.ListTerminalProfile);
        if (loaded == null || loaded.Count == 0) return false;

        ReplaceProfiles(loaded.Select(NormalizeProfile));
        return true;
    }

    private void ReplaceProfiles(IEnumerable<TerminalProfile> profiles)
    {
        lock (_profilesLock)
        {
            _profiles.Clear();
            _profiles.AddRange(profiles);
        }
    }

    public IReadOnlyList<TerminalProfile> GetProfiles()
    {
        lock (_profilesLock)
        {
            return _profiles.ToList();
        }
    }

    public TerminalProfile? GetProfile(Guid id)
    {
        lock (_profilesLock)
        {
            return _profiles.FirstOrDefault(p => p.Id == id);
        }
    }

    public async Task AddProfileAsync(TerminalProfile profile, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProfile(profile);
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_profilesLock)
            {
                _profiles.Add(normalized);
            }
            await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public async Task UpdateProfileAsync(TerminalProfile profile, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeProfile(profile);
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_profilesLock)
            {
                var index = _profiles.FindIndex(p => p.Id == normalized.Id);
                if (index >= 0)
                {
                    _profiles[index] = normalized;
                }
            }
            await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public async Task<bool> DeleteProfileAsync(Guid id, CancellationToken cancellationToken = default)
    {
        bool removed = false;
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            lock (_profilesLock)
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
                await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
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

    public async Task ResetToDefaultsAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ReplaceProfiles(CreateDefaultProfiles());
            await SaveInternalAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
        ProfilesChanged?.Invoke();
    }

    public async Task LoadProfilesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            TryApplyLoadedProfiles(json);
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
        var defaultDir = GetDefaultWorkingDirectory();
        var pwshPath = ShellDiscoveryService.ResolveExecutable("pwsh.exe");
        var winPsPath = ShellDiscoveryService.ResolveExecutable("powershell.exe");

        var list = new List<TerminalProfile>
        {
            new(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                pwshPath != null ? "PowerShell 7" : "Windows PowerShell",
                pwshPath ?? winPsPath ?? "powershell.exe",
                Arguments: "-NoLogo",
                WorkingDirectory: defaultDir,
                IconTag: "PS",
                ShellType: ShellType.PowerShell,
                IsBuiltIn: true),
            new(
                Guid.Parse("22222222-2222-2222-2222-222222222222"),
                _localizationService?["Shell_CMD"] ?? "Command Prompt",
                ShellDiscoveryService.ResolveExecutable("cmd.exe") ?? "cmd.exe",
                Arguments: null,
                WorkingDirectory: defaultDir,
                IconTag: "CMD",
                ShellType: ShellType.CMD,
                IsBuiltIn: true)
        };

        TryAddOptionalProfile(list, "33333333-3333-3333-3333-333333333333", "WSL (Linux)", "wsl.exe", "WSL", ShellType.WSL, defaultDir);
        TryAddOptionalProfile(list, "44444444-4444-4444-4444-444444444444", "NuShell", "nu.exe", "NU", ShellType.NuShell, defaultDir);

        return list;
    }

    private static void TryAddOptionalProfile(
        List<TerminalProfile> profiles,
        string id,
        string name,
        string executableName,
        string iconTag,
        ShellType shellType,
        string defaultDir)
    {
        var resolvedPath = ShellDiscoveryService.ResolveExecutable(executableName);
        if (resolvedPath == null) return;

        profiles.Add(new TerminalProfile(
            Guid.Parse(id),
            name,
            resolvedPath,
            Arguments: null,
            WorkingDirectory: defaultDir,
            IconTag: iconTag,
            ShellType: shellType,
            IsBuiltIn: true));
    }

    private async Task SaveInternalAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            List<TerminalProfile> copy;
            lock (_profilesLock)
            {
                copy = _profiles.ToList();
            }

            var json = JsonSerializer.Serialize(copy, MultiShellJsonSerializerContext.Default.ListTerminalProfile);
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save terminal profiles: {ex.Message}");
        }
    }
}
