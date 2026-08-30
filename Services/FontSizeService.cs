using System;

namespace MultiShell.Services;

/// <summary>
/// Implementation of <see cref="IFontSizeService"/> providing 5-level font size management for App UI and Terminal.
/// Level 3 is the system default baseline (App: 1.0x, Terminal: 12.0 pt).
/// </summary>
public class FontSizeService : IFontSizeService
{
    private static readonly double[] AppScaleLevels = [0.85, 0.92, 1.00, 1.12, 1.25];
    private static readonly double[] TerminalFontSizes = [9.5, 10.5, 12.0, 14.0, 16.5];

    private int _appFontSizeLevel = IFontSizeService.DefaultLevel;
    private int _terminalFontSizeLevel = IFontSizeService.DefaultLevel;

    public event Action<int>? AppFontSizeLevelChanged;
    public event Action<int>? TerminalFontSizeLevelChanged;

    public int AppFontSizeLevel
    {
        get => _appFontSizeLevel;
        set => SetAppFontSizeLevel(value);
    }

    public int TerminalFontSizeLevel
    {
        get => _terminalFontSizeLevel;
        set => SetTerminalFontSizeLevel(value);
    }

    public double AppFontScale => GetAppFontScale(_appFontSizeLevel);

    public double TerminalFontSize => GetTerminalFontSize(_terminalFontSizeLevel);

    public FontSizeService(int initialAppLevel = IFontSizeService.DefaultLevel, int initialTerminalLevel = IFontSizeService.DefaultLevel)
    {
        _appFontSizeLevel = Math.Clamp(initialAppLevel, IFontSizeService.MinLevel, IFontSizeService.MaxLevel);
        _terminalFontSizeLevel = Math.Clamp(initialTerminalLevel, IFontSizeService.MinLevel, IFontSizeService.MaxLevel);
    }

    public double GetAppFontScale(int level)
    {
        var index = Math.Clamp(level, IFontSizeService.MinLevel, IFontSizeService.MaxLevel) - 1;
        return AppScaleLevels[index];
    }

    public double GetTerminalFontSize(int level)
    {
        var index = Math.Clamp(level, IFontSizeService.MinLevel, IFontSizeService.MaxLevel) - 1;
        return TerminalFontSizes[index];
    }

    public void SetAppFontSizeLevel(int level)
    {
        var clamped = Math.Clamp(level, IFontSizeService.MinLevel, IFontSizeService.MaxLevel);
        if (_appFontSizeLevel == clamped) return;

        _appFontSizeLevel = clamped;
        AppFontSizeLevelChanged?.Invoke(_appFontSizeLevel);
    }

    public void SetTerminalFontSizeLevel(int level)
    {
        var clamped = Math.Clamp(level, IFontSizeService.MinLevel, IFontSizeService.MaxLevel);
        if (_terminalFontSizeLevel == clamped) return;

        _terminalFontSizeLevel = clamped;
        TerminalFontSizeLevelChanged?.Invoke(_terminalFontSizeLevel);
    }

    public void ResetLevels()
    {
        SetAppFontSizeLevel(IFontSizeService.DefaultLevel);
        SetTerminalFontSizeLevel(IFontSizeService.DefaultLevel);
    }

    public void ResetAppFontSizeLevel()
    {
        SetAppFontSizeLevel(IFontSizeService.DefaultLevel);
    }

    public void ResetTerminalFontSizeLevel()
    {
        SetTerminalFontSizeLevel(IFontSizeService.DefaultLevel);
    }
}
