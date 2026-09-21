using Avalonia;
using System;
using MultiShell.Services;

namespace MultiShell;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        using var singleInstanceService = new SingleInstanceService();
        if (!singleInstanceService.IsFirstInstance)
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
            singleInstanceService.StartServer();
            App.SingleInstance = singleInstanceService;

            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Server stops and releases mutex when app lifetime ends
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int dwProcessId);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
