using Avalonia.Input;
using MultiShell.Views;
using Xunit;

namespace MultiShell.Tests;

public class FunctionKeyRoutingTests
{
    [Theory]
    [InlineData(Key.F1)]
    [InlineData(Key.F2)]
    [InlineData(Key.F3)]
    [InlineData(Key.F4)]
    [InlineData(Key.F5)]
    [InlineData(Key.F6)]
    [InlineData(Key.F7)]
    [InlineData(Key.F8)]
    [InlineData(Key.F9)]
    [InlineData(Key.F10)]
    [InlineData(Key.F11)]
    [InlineData(Key.F12)]
    [InlineData(Key.F13)]
    [InlineData(Key.F14)]
    [InlineData(Key.F15)]
    [InlineData(Key.F16)]
    [InlineData(Key.F17)]
    [InlineData(Key.F18)]
    [InlineData(Key.F19)]
    [InlineData(Key.F20)]
    [InlineData(Key.F21)]
    [InlineData(Key.F22)]
    [InlineData(Key.F23)]
    [InlineData(Key.F24)]
    public void IsFunctionKey_ReturnsTrue_ForFunctionKeysF1ThroughF24(Key key)
    {
        // Act
        var result = FunctionKeyRouting.IsFunctionKey(key);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Z)]
    [InlineData(Key.D1)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Escape)]
    [InlineData(Key.Tab)]
    [InlineData(Key.W)]
    [InlineData(Key.H)]
    [InlineData(Key.L)]
    [InlineData(Key.PageUp)]
    [InlineData(Key.PageDown)]
    public void IsFunctionKey_ReturnsFalse_ForNonFunctionKeys(Key key)
    {
        // Act
        var result = FunctionKeyRouting.IsFunctionKey(key);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(Key.F1)]
    [InlineData(Key.F2)]
    [InlineData(Key.F3)]
    [InlineData(Key.F4)]
    [InlineData(Key.F5)]
    [InlineData(Key.F10)]
    [InlineData(Key.F12)]
    public void ShouldInterceptFunctionKey_ReturnsFalse_WhenNoModifiersPressed(Key key)
    {
        // Act - Plain function key press must pass through to terminal
        var result = FunctionKeyRouting.ShouldInterceptFunctionKey(key, KeyModifiers.None);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(Key.F1, KeyModifiers.Shift)]
    [InlineData(Key.F2, KeyModifiers.Shift)]
    [InlineData(Key.F3, KeyModifiers.Shift)]
    [InlineData(Key.F4, KeyModifiers.Control)]
    [InlineData(Key.F5, KeyModifiers.Control)]
    [InlineData(Key.F10, KeyModifiers.Alt)]
    [InlineData(Key.F12, KeyModifiers.Alt)]
    [InlineData(Key.F1, KeyModifiers.Control | KeyModifiers.Alt)]
    [InlineData(Key.F2, KeyModifiers.Shift | KeyModifiers.Alt)]
    public void ShouldInterceptFunctionKey_ReturnsFalse_WhenOnlySingleOrNonCtrlShiftModifiers(Key key, KeyModifiers modifiers)
    {
        // Act - Function key without both Ctrl and Shift must pass through to terminal
        var result = FunctionKeyRouting.ShouldInterceptFunctionKey(key, modifiers);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(Key.F1)]
    [InlineData(Key.F2)]
    [InlineData(Key.F3)]
    [InlineData(Key.F4)]
    [InlineData(Key.F5)]
    [InlineData(Key.F12)]
    public void ShouldInterceptFunctionKey_ReturnsTrue_WhenCtrlAndShiftPressedSimultaneously(Key key)
    {
        // Act - Function key with Ctrl+Shift must be intercepted by MultiShell
        var result = FunctionKeyRouting.ShouldInterceptFunctionKey(key, KeyModifiers.Control | KeyModifiers.Shift);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData(Key.F1)]
    [InlineData(Key.F2)]
    [InlineData(Key.F3)]
    [InlineData(Key.F4)]
    public void ShouldInterceptFunctionKey_ReturnsFalse_WhenAltAlsoPressedWithCtrlShift(Key key)
    {
        // Act - Alt modifier takes precedence, passing through to terminal
        var result = FunctionKeyRouting.ShouldInterceptFunctionKey(key, KeyModifiers.Control | KeyModifiers.Shift | KeyModifiers.Alt);

        // Assert
        Assert.False(result);
    }

    [Theory]
    [InlineData(Key.A)]
    [InlineData(Key.Enter)]
    [InlineData(Key.Tab)]
    [InlineData(Key.Escape)]
    public void ShouldInterceptFunctionKey_ReturnsFalse_ForNonFunctionKeysEvenWithCtrlShift(Key key)
    {
        // Act
        var result = FunctionKeyRouting.ShouldInterceptFunctionKey(key, KeyModifiers.Control | KeyModifiers.Shift);

        // Assert
        Assert.False(result);
    }
}
