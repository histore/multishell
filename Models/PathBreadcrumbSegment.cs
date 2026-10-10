namespace MultiShell.Models;

/// <summary>
/// Represents an interactive segment within a directory path breadcrumb bar (REQ-TERM-013).
/// </summary>
/// <param name="Name">The display label of the segment (e.g. "C:", "projekte", "multishell").</param>
/// <param name="FullPath">The cumulative full path up to and including this segment.</param>
/// <param name="IsCurrent">Whether this segment represents the leaf / current active directory.</param>
/// <param name="Tooltip">The dynamic contextual tooltip depending on command draft state and current segment status.</param>
public sealed record PathBreadcrumbSegment(
    string Name,
    string FullPath,
    bool IsCurrent,
    string Tooltip = "");
