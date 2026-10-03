namespace MultiShell.Models;

/// <summary>
/// Represents a single color stripe segment derived from a directory path level (REQ-TAB-025).
/// Has a fixed base size (width = 9.0, height = 3.0) and scales down dynamically when tab width is constrained.
/// Encapsulates color strictly as an Avalonia-agnostic hex string (#RRGGBB) to preserve Clean Architecture domain isolation.
/// </summary>
/// <param name="FolderName">The folder name corresponding to this stripe segment.</param>
/// <param name="HexColor">The deterministic hex color string (#RRGGBB) calculated for this folder.</param>
/// <param name="Width">The fixed base width in pixels (default 9.0).</param>
/// <param name="Height">The fixed height in pixels (default 3.0).</param>
public sealed record PathColorStripe(
    string FolderName,
    string HexColor,
    double Width = 9.0,
    double Height = 3.0);
