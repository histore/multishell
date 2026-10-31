using Avalonia;
using System;
using System.Linq;
using MultiShell.Services;

namespace MultiShell;

sealed partial class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        bool isMultiInstance = args.Any(a => string.Equals(a, "--multi-instance", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "--new-instance", StringComparison.OrdinalIgnoreCase) ||
                                             string.Equals(a, "-m", StringComparison.OrdinalIgnoreCase));

        using var singleInstanceService = isMultiInstance ? null : new SingleInstanceService();
        if (singleInstanceService != null && !singleInstanceService.IsFirstInstance)
        {
            // Allow the primary running instance to bring its window to the foreground on Windows
            if (OperatingSystem.IsWindows())
            {
                AllowSetForegroundWindow(-1);
            }

            // Send args to first instance and exit immediately
            try
            {
                singleInstanceService.SendArgsToFirstInstanceAsync(args, Environment.CurrentDirectory)
                    .GetAwaiter().GetResult();
            }
            catch
            {
                // Fallback: ignore pipe send exceptions on early client exit
            }
            return;
        }

        try
        {
            if (singleInstanceService != null)
            {
                singleInstanceService.StartServer();
                App.SingleInstance = singleInstanceService;
            }

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Server stops and releases mutex when app lifetime ends
        }
    }

    [System.Runtime.InteropServices.LibraryImport("user32.dll", SetLastError = true)]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static partial bool AllowSetForegroundWindow(int dwProcessId);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .With(new Win32PlatformOptions
            {
                RenderingMode = [Win32RenderingMode.AngleEgl, Win32RenderingMode.Vulkan, Win32RenderingMode.Wgl, Win32RenderingMode.Software],
                CompositionMode = [Win32CompositionMode.WinUIComposition, Win32CompositionMode.DirectComposition, Win32CompositionMode.LowLatencyDxgiSwapChain, Win32CompositionMode.RedirectionSurface],
                OverlayPopups = true
            })
            .With(new X11PlatformOptions
            {
                RenderingMode = [X11RenderingMode.Vulkan, X11RenderingMode.Egl, X11RenderingMode.Glx, X11RenderingMode.Software],
                OverlayPopups = true,
                UseDBusMenu = true,
                EnableMultiTouch = true
            })
            .With(new AvaloniaNativePlatformOptions
            {
                RenderingMode = [AvaloniaNativeRenderingMode.Metal, AvaloniaNativeRenderingMode.OpenGl, AvaloniaNativeRenderingMode.Software]
            })
            .With(new MacOSPlatformOptions
            {
                ShowInDock = true
            })
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
