using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace MultiShell.Services;

/// <summary>
/// Default implementation of <see cref="IThemeService"/> allowing independent UI and terminal theme control.
/// </summary>
public class ThemeService : IThemeService
{
    private static readonly SolidColorBrush DarkTerminalBackground = new(Color.Parse("#0E0F15"));
    private static readonly SolidColorBrush LightTerminalBackground = new(Color.Parse("#F8F9FC"));
    private static readonly SolidColorBrush DarkTerminalCaret = new(Color.Parse("#7AA2F7"));
    private static readonly SolidColorBrush LightTerminalCaret = new(Color.Parse("#2563EB"));

    private static readonly SolidColorBrush[] DarkTerminalPalette =
    [
        new(Color.Parse("#0E0F15")), // 0: Dark Background
        new(Color.Parse("#F7768E")), // 1: Red
        new(Color.Parse("#9ECE6A")), // 2: Green
        new(Color.Parse("#E0AF68")), // 3: Yellow
        new(Color.Parse("#7AA2F7")), // 4: Blue
        new(Color.Parse("#BB9AF7")), // 5: Magenta
        new(Color.Parse("#7DCFFF")), // 6: Cyan
        new(Color.Parse("#C0CAF5")), // 7: Foreground Text
        new(Color.Parse("#414868")), // 8: Gray
        new(Color.Parse("#F7768E")), // 9: Bright Red
        new(Color.Parse("#9ECE6A")), // 10: Bright Green
        new(Color.Parse("#E0AF68")), // 11: Bright Yellow
        new(Color.Parse("#7AA2F7")), // 12: Bright Blue
        new(Color.Parse("#BB9AF7")), // 13: Bright Magenta
        new(Color.Parse("#7DCFFF")), // 14: Bright Cyan
        new(Color.Parse("#FFFFFF"))  // 15: Bright White
    ];

    private static readonly SolidColorBrush[] LightTerminalPalette =
    [
        new(Color.Parse("#F8F9FC")), // 0: Light Background
        new(Color.Parse("#D32F2F")), // 1: Red
        new(Color.Parse("#2E7D32")), // 2: Green
        new(Color.Parse("#E65100")), // 3: Dark Yellow / Orange
        new(Color.Parse("#1976D2")), // 4: Blue
        new(Color.Parse("#7B1FA2")), // 5: Magenta
        new(Color.Parse("#0097A7")), // 6: Cyan
        new(Color.Parse("#1A1D2B")), // 7: Dark Foreground Text
        new(Color.Parse("#757D96")), // 8: Gray
        new(Color.Parse("#C62828")), // 9: Bright Red
        new(Color.Parse("#1B5E20")), // 10: Bright Green
        new(Color.Parse("#BF360C")), // 11: Bright Yellow
        new(Color.Parse("#0D47A1")), // 12: Bright Blue
        new(Color.Parse("#4A148C")), // 13: Bright Magenta
        new(Color.Parse("#006064")), // 14: Bright Cyan
        new(Color.Parse("#0A0B10"))  // 15: Bright Black / Dark Text
    ];

    private bool _isDarkTerminalTheme = true;

    public bool IsDarkAppTheme => Application.Current?.RequestedThemeVariant != ThemeVariant.Light;

    public bool IsDarkTerminalTheme => _isDarkTerminalTheme;

    public void SetAppTheme(bool isDark)
    {
        void Apply()
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = isDark ? ThemeVariant.Dark : ThemeVariant.Light;
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }

    public void SetTerminalTheme(bool isDark)
    {
        _isDarkTerminalTheme = isDark;

        void Apply()
        {
            if (Application.Current != null)
            {
                Application.Current.Resources["TerminalSurfaceBackground"] = isDark ? DarkTerminalBackground : LightTerminalBackground;
                Application.Current.Resources["TerminalCaretBrush"] = isDark ? DarkTerminalCaret : LightTerminalCaret;

                var palette = isDark ? DarkTerminalPalette : LightTerminalPalette;
                for (var i = 0; i < palette.Length; i++)
                {
                    Application.Current.Resources[$"SvcSystems.UI.TerminalColor{i}"] = palette[i];
                }
            }
        }


        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }

    public void ToggleAppTheme()
    {
        SetAppTheme(!IsDarkAppTheme);
    }

    public void ToggleTerminalTheme()
    {
        SetTerminalTheme(!IsDarkTerminalTheme);
    }
}
