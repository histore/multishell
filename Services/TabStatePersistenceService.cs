using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Persists and loads workspace tab states using JSON files in local app data.
/// Thread-safe and atomic to preserve exact tab order across parallel events and restarts.
/// </summary>
public class TabStatePersistenceService : ITabStatePersistenceService
{
    private readonly string _filePath;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public TabStatePersistenceService(string? customFilePath = null)
    {
        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var appDir = Path.Combine(localAppData, "MultiShell");
            _filePath = Path.Combine(appDir, "tabs_state.json");
        }
    }

    public async Task SaveStateAsync(WorkspaceState state)
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(state, MultiShellJsonSerializerContext.Default.WorkspaceState);
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save tab state: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<WorkspaceState?> LoadStateAsync()
    {
        await _semaphore.WaitAsync().ConfigureAwait(false);
        try
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize(json, MultiShellJsonSerializerContext.Default.WorkspaceState);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load tab state: {ex.Message}");
            return null;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
