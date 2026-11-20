using System;

namespace MultiShell.Services;

/// <summary>
/// Platform integration abstraction for OS-level windowing, keyboard toggle queries, and URL launching.
/// </summary>
public interface IPlatformService
{
    /// <summary>
    /// Checks whether Caps Lock is currently toggled on.
    /// </summary>
    bool QueryCapsLockState();

    /// <summary>
    /// Brings the specified native window handle to the foreground when supported by the OS.
    /// </summary>
    void SetWindowForeground(IntPtr windowHandle);

    /// <summary>
    /// Opens an external HTTP/HTTPS URL in the user's default browser.
    /// </summary>
    bool OpenUrl(string url);
}
