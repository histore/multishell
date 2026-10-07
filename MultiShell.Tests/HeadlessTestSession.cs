using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Headless;

namespace MultiShell.Tests;

public static class HeadlessTestSession
{
    private static HeadlessUnitTestSession? _session;
    private static readonly Lock _initLock = new();

    public static HeadlessUnitTestSession GetSession()
    {
        if (_session != null) return _session;
        lock (_initLock)
        {
            if (_session != null) return _session;
            _session = HeadlessUnitTestSession.StartNew(typeof(TestAppBuilder));
            return _session;
        }
    }

    public static Task DispatchAsync(System.Action action)
    {
        return GetSession().Dispatch(action, default);
    }

    public static Task<T> DispatchAsync<T>(System.Func<T> func)
    {
        return GetSession().Dispatch(func, default);
    }
}

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
