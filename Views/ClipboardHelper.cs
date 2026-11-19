using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using MultiShell.ViewModels;

namespace MultiShell.Views;

/// <summary>
/// Shared clipboard read/write helpers with defensive error handling for locked Windows clipboard handles.
/// </summary>
internal static class ClipboardHelper
{
    internal static async Task TrySetTextAsync(IClipboard? clipboard, string text, string logContext)
    {
        if (clipboard == null || string.IsNullOrEmpty(text)) return;

        try
        {
            await clipboard.SetTextAsync(text);
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
        {
            System.Diagnostics.Debug.WriteLine($"[{logContext}] Clipboard copy failed: {ex.Message}");
        }
    }

    internal static async Task<string?> TryGetTextAsync(IClipboard? clipboard, string logContext)
    {
        if (clipboard == null) return null;

        try
        {
            return await clipboard.TryGetTextAsync();
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
        {
            System.Diagnostics.Debug.WriteLine($"[{logContext}] Clipboard paste failed: {ex.Message}");
            return null;
        }
    }

    internal static async Task PasteToPaneAsync(TerminalPaneViewModel pane, Func<Task<string?>> readClipboardTextAsync, string logContext)
    {
        if (!pane.IsRunning) return;

        try
        {
            var text = await readClipboardTextAsync();
            if (!string.IsNullOrEmpty(text))
            {
                pane.PasteText(text);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or COMException)
        {
            System.Diagnostics.Debug.WriteLine($"[{logContext}] Clipboard paste failed: {ex.Message}");
        }
    }
}
