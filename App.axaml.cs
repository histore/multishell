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
            var resolver = new StartupPathResolver();
            var initialDir = resolver.ResolveFromArgs(desktop.Args);

            var coordinator = WindowCoordinatorService.Instance;
            var vm = new MainViewModel(windowCoordinator: coordinator, initialDirectory: initialDir);
            coordinator.RegisterWindow(vm, isPrimary: true);

            var window = new MainWindow
            {
                DataContext = vm,
            };
            desktop.MainWindow = window;
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnLastWindowClose;

            coordinator.WindowOpenRequested += (_, args) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    var satelliteWindow = new MainWindow
                    {
                        DataContext = args.ViewModel,
                    };
                    if (args.ScreenX.HasValue && args.ScreenY.HasValue)
                    {
                        satelliteWindow.Position = new PixelPoint((int)args.ScreenX.Value, (int)args.ScreenY.Value);
                    }
                    satelliteWindow.Show();
                });
            };

            coordinator.WindowCloseRequested += (_, args) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    foreach (var openWindow in desktop.Windows)
                    {
                        if (openWindow.DataContext == args.ViewModel)
                        {
                            openWindow.Close();
                            break;
                        }
                    }
                });
            };

            coordinator.ApplicationShutdownRequested += (_, _) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    desktop.Shutdown();
                });
            };

            desktop.Exit += (_, _) => vm.Dispose();
            window.Closed += (_, _) => vm.Dispose();

            if (SingleInstance != null)
            {
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

        base.OnFrameworkInitializationCompleted();
    }
}