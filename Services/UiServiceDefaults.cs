namespace MultiShell.Services;

/// <summary>
/// Composition-root defaults for UI-layer services consumed by ViewModels without referencing
/// concrete Avalonia types or asset URIs inside the Presentation/ViewModel layer (BL-MED-007).
/// </summary>
public static class UiServiceDefaults
{
    public const string DefaultTerminalFontFamily =
        "avares://MultiShell/Assets/Fonts#FiraCode Nerd Font Mono, Cascadia Code NF, CascadiaMono NF, CaskaydiaMono Nerd Font, Cascadia Mono, Cascadia Code, Consolas, Segoe UI Symbol, Segoe UI Emoji, DejaVu Sans Mono, monospace";

    public static IDispatcherService DispatcherService { get; set; } = AvaloniaDispatcherService.Instance;

    public static ITerminalModelFactory TerminalModelFactory { get; set; } = AvaloniaTerminalModelFactory.Instance;
}
