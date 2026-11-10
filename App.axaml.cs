using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using MultiShell.Services;
using MultiShell.ViewModels;
using MultiShell.Views;

namespace MultiShell;

public partial class App : Application
{
    public static ISingleInstanceService? SingleInstance { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            InitializeDesktopApplication(desktop);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void InitializeDesktopApplication(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var resolver = new StartupPathResolver();
        var initialDir = resolver.ResolveFromArgs(desktop.Args);

        var coordinator = WindowCoordinatorService.Instance;
        var vm = new MainViewModel(windowCoordinator: coordinator, initialDirectory: initialDir);
        coordinator.RegisterWindow(vm, isPrimary: true);

        var window = new MainWindow { DataContext = vm };
        desktop.MainWindow = window;
        desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnLastWindowClose;

        WireWindowCoordinator(desktop, coordinator);
        desktop.Exit += (_, _) => vm.Dispose();
        window.Closed += (_, _) => vm.Dispose();
        WireSingleInstance(window, vm);
    }

    private static void WireWindowCoordinator(IClassicDesktopStyleApplicationLifetime desktop, WindowCoordinatorService coordinator)
    {
        coordinator.WindowOpenRequested += (_, args) =>
        {
            Dispatcher.UIThread.Post(() => OpenSatelliteWindow(args));
        };

        coordinator.WindowCloseRequested += (_, args) =>
        {
            Dispatcher.UIThread.Post(() => CloseCoordinatedWindow(desktop, args.ViewModel));
        };

        coordinator.ApplicationShutdownRequested += (_, _) =>
        {
            Dispatcher.UIThread.Post(() => desktop.Shutdown());
        };
    }

    private static void OpenSatelliteWindow(WindowOpenRequestedEventArgs args)
    {
        var satelliteWindow = new MainWindow { DataContext = args.ViewModel };
        if (args.ScreenX.HasValue && args.ScreenY.HasValue)
        {
            satelliteWindow.Position = new PixelPoint((int)args.ScreenX.Value, (int)args.ScreenY.Value);
        }
        satelliteWindow.Show();
    }

    private static void CloseCoordinatedWindow(IClassicDesktopStyleApplicationLifetime desktop, ICoordinatedWindow viewModel)
    {
        foreach (var openWindow in desktop.Windows)
        {
            if (openWindow.DataContext != viewModel) continue;
            openWindow.Close();
            break;
        }
    }

    private static void WireSingleInstance(MainWindow window, MainViewModel vm)
    {
        if (SingleInstance == null) return;

        SingleInstance.DirectoryOpenRequested += path =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                window.BringToForeground();
                if (!string.IsNullOrWhiteSpace(path))
                {
                    vm.AddNewTabWithDirectory(path, vm.DefaultShellType);
                }
            });
        };
    }
}