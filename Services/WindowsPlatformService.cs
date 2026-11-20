using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MultiShell.Services;

/// <summary>
/// Default Windows/cross-platform implementation of <see cref="IPlatformService"/>.
/// </summary>
public sealed partial class WindowsPlatformService : IPlatformService
{
    public static WindowsPlatformService Instance { get; } = new();

    private WindowsPlatformService()
    {
    }

    public bool QueryCapsLockState() => KeyboardStateHelper.QueryCapsLockState();

    public void SetWindowForeground(IntPtr windowHandle)
    {
        if (windowHandle != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            SetForegroundWindow(windowHandle);
        }
    }

    public bool OpenUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = uri.AbsoluteUri,
                UseShellExecute = true
            };
            using var proc = Process.Start(psi);
            return true;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or NotSupportedException)
        {
            Debug.WriteLine($"[WindowsPlatformService.OpenUrl] Browser launch failed: {ex.Message}");
            return false;
        }
    }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(IntPtr hWnd);
}
