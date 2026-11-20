using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace MultiShell.Views;

/// <summary>
/// Centralizes Avalonia <see cref="IStorageProvider"/> file/folder picker invocation and error handling.
/// </summary>
internal static class StoragePickerHelper
{
    internal static async Task<string?> PickSingleFileAsync(
        Visual owner,
        string title,
        string? initialDirectory,
        IReadOnlyList<FilePickerFileType>? fileTypeFilter = null,
        string logContext = "StoragePickerHelper")
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider == null) return null;

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = fileTypeFilter,
            SuggestedStartLocation = await TryResolveStartFolderAsync(topLevel.StorageProvider, initialDirectory, logContext)
        };

        try
        {
            var results = await topLevel.StorageProvider.OpenFilePickerAsync(options);
            return results.Count > 0 ? results[0].TryGetLocalPath() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or COMException)
        {
            System.Diagnostics.Debug.WriteLine($"[{logContext}] File picker failed: {ex.Message}");
            return null;
        }
    }

    internal static async Task<string?> PickSingleFolderAsync(
        Visual owner,
        string title,
        string? initialDirectory,
        string logContext = "StoragePickerHelper")
    {
        var topLevel = TopLevel.GetTopLevel(owner);
        if (topLevel?.StorageProvider == null) return null;

        var options = new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = await TryResolveStartFolderAsync(topLevel.StorageProvider, initialDirectory, logContext)
        };

        try
        {
            var results = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            return results.Count > 0 ? results[0].TryGetLocalPath() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or COMException)
        {
            System.Diagnostics.Debug.WriteLine($"[{logContext}] Folder picker failed: {ex.Message}");
            return null;
        }
    }

    private static async Task<IStorageFolder?> TryResolveStartFolderAsync(
        IStorageProvider storageProvider,
        string? initialDirectory,
        string logContext)
    {
        var resolvedDir = !string.IsNullOrWhiteSpace(initialDirectory) &&
                          MultiShell.Services.LinkDetectionHelper.IsSafeLocalPath(initialDirectory) &&
                          Directory.Exists(initialDirectory)
            ? initialDirectory
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (string.IsNullOrWhiteSpace(resolvedDir) ||
            !MultiShell.Services.LinkDetectionHelper.IsSafeLocalPath(resolvedDir) ||
            !Directory.Exists(resolvedDir))
        {
            return null;
        }

        try
        {
            return await storageProvider.TryGetFolderFromPathAsync(resolvedDir);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine($"[{logContext}] StorageProvider suggested folder lookup failed: {ex.Message}");
            return null;
        }
    }
}
