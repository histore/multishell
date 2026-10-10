using System;
using System.Runtime.InteropServices;

namespace MultiShell.Services;

/// <summary>
/// Native interop helper for querying hardware keyboard toggle states (Caps Lock, Num Lock).
/// </summary>
public static partial class KeyboardStateHelper
{
    private const int VkCapital = 0x14;

    [LibraryImport("user32.dll")]
    private static partial short GetKeyState(int nVirtKey);

    /// <summary>
    /// Checks whether Caps Lock is currently toggled on in the operating system.
    /// </summary>
    public static bool QueryCapsLockState()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                return (GetKeyState(VkCapital) & 1) != 0;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
