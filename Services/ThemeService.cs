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
