using System.Threading.Tasks;
using Avalonia;
using Avalonia.Styling;
using MultiShell.Services;
using Xunit;

namespace MultiShell.Tests;

[Collection("HeadlessUI")]
public class ThemeServiceTests
{
    [Fact]
    public async Task ThemeService_SetTerminalTheme_UpdatesThemeAndResources()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var service = new ThemeService();

            service.SetTerminalTheme(false);
            Assert.False(service.IsDarkTerminalTheme);

            service.SetTerminalTheme(true);
            Assert.True(service.IsDarkTerminalTheme);
        });
    }

    [Fact]
    public async Task ThemeService_ToggleTerminalTheme_InvertsTheme()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var service = new ThemeService();
            bool initial = service.IsDarkTerminalTheme;

            service.ToggleTerminalTheme();
            Assert.Equal(!initial, service.IsDarkTerminalTheme);

            service.ToggleTerminalTheme();
            Assert.Equal(initial, service.IsDarkTerminalTheme);
        });
    }

    [Fact]
    public async Task ThemeService_SetAppTheme_UpdatesApplicationThemeVariant()
    {
        await HeadlessTestSession.DispatchAsync(() =>
        {
            var service = new ThemeService();

            service.SetAppTheme(false);
            if (Application.Current != null)
            {
                Assert.Equal(ThemeVariant.Light, Application.Current.RequestedThemeVariant);
            }

            service.SetAppTheme(true);
            if (Application.Current != null)
            {
                Assert.Equal(ThemeVariant.Dark, Application.Current.RequestedThemeVariant);
            }
        });
    }
}
