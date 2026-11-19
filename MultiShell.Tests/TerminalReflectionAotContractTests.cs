using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using MultiShell.Services;
using MultiShell.Views;
using SvcSystems.UI.Terminal;
using Xunit;

namespace MultiShell.Tests;

/// <summary>
/// Guards private reflection targets on SvcSystems.UI.Terminal / XTerm.NET against silent breakage
/// across package updates and NativeAOT trimming (BL-HIGH-006).
/// </summary>
public class TerminalReflectionAotContractTests
{
    [Fact]
    public void XTermEngine_MouseTrackerMembers_ExistAndArePreservedByDynamicDependency()
    {
        using var adapter = new AvaloniaTerminalModelAdapter();
        var engine = adapter.Model.Terminal.Engine;
        var engineType = engine.GetType();

        Assert.Equal("XTerm.Terminal", engineType.FullName);

        var trackerField = engineType.GetField("_mouseTracker", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(trackerField);

        var tracker = trackerField!.GetValue(engine);
        Assert.NotNull(tracker);
        Assert.Equal("XTerm.Input.MouseTracker", tracker!.GetType().FullName);

        var trackingProp = tracker.GetType().GetProperty("TrackingMode", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(trackingProp);

        var suppressMethod = typeof(AvaloniaTerminalModelAdapter).GetMethod(nameof(AvaloniaTerminalModelAdapter.SuppressMouseTracking));
        Assert.NotNull(suppressMethod);
        var deps = suppressMethod!.GetCustomAttributes<DynamicDependencyAttribute>().ToList();
        Assert.Contains(deps, d => d.TypeName == "XTerm.Terminal" && d.AssemblyName == "XTerm.NET");
        Assert.Contains(deps, d => d.TypeName == "XTerm.Input.MouseTracker" && d.AssemblyName == "XTerm.NET");
    }

    [Fact]
    public void TerminalControl_ReflectedThemeAndMetricMembers_ExistAndArePreservedByDynamicDependency()
    {
        var controlType = typeof(TerminalControl);
        Assert.NotNull(controlType.GetField("FallbackXtermPalette", BindingFlags.NonPublic | BindingFlags.Static));
        Assert.NotNull(controlType.GetField("_surface", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(controlType.GetField("_consoleTextSize", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance));

        var hasClearCacheMethod = controlType.GetMethod("ClearFormattedTextCache", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        var hasCacheField = controlType.GetField("_formattedTextCache", BindingFlags.NonPublic | BindingFlags.Instance) != null;
        Assert.True(hasClearCacheMethod || hasCacheField);

        var applyThemeMethod = typeof(TerminalPaneView).GetMethod("ApplyTerminalTheme", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(applyThemeMethod);
        Assert.Contains(applyThemeMethod!.GetCustomAttributes<DynamicDependencyAttribute>(), d => d.Type == typeof(TerminalControl));

        var hitTestMethod = typeof(TerminalPaneView).GetMethod(nameof(TerminalPaneView.TryGetTerminalWordOrLine), BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(hitTestMethod);
        Assert.Contains(hitTestMethod!.GetCustomAttributes<DynamicDependencyAttribute>(), d => d.Type == typeof(TerminalControl));
    }
}
