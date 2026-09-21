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

            var vm = new MainViewModel(initialDirectory: initialDir);
            var window = new MainWindow
            {
                DataContext = vm,
            };
            desktop.MainWindow = window;

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