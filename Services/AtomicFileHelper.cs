using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MultiShell.Services;

/// <summary>
/// Provides crash-safe atomic file writes using process/operation-unique temporary swap files
/// to prevent collisions across concurrent --multi-instance processes.
/// </summary>
internal static class AtomicFileHelper
{
    public static async Task WriteAllTextAtomicAsync(string filePath, string content, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = $"{filePath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(tempPath, content, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, filePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    System.Diagnostics.Debug.WriteLine($"[AtomicFileHelper] Failed to delete temporary file '{tempPath}': {ex.Message}");
                }
            }
        }
    }
}
