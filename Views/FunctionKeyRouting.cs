using Avalonia.Input;

namespace MultiShell.Views;

/// <summary>
/// Provides evaluation logic for function key (F1..F24) routing.
/// In accordance with terminal emulation requirements, function keys are fundamentally passed
/// through to the terminal, and are intercepted by MultiShell strictly when pressed with Ctrl and Shift.
/// </summary>
public static class FunctionKeyRouting
{
    /// <summary>
    /// Determines whether the specified key is a function key (F1 through F24).
    /// </summary>
    public static bool IsFunctionKey(Key key) => key is >= Key.F1 and <= Key.F24;

    /// <summary>
    /// Determines whether a function key should be intercepted by MultiShell.
    /// Returns true strictly when the key is a function key and both Control and Shift are active without Alt.
    /// </summary>
    public static bool ShouldInterceptFunctionKey(Key key, KeyModifiers modifiers)
    {
        if (!IsFunctionKey(key))
        {
            return false;
        }

        var isAlt = (modifiers & KeyModifiers.Alt) != 0;
        var isCtrlShift = (modifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == (KeyModifiers.Control | KeyModifiers.Shift);
        return !isAlt && isCtrlShift;
    }
}
