using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using MultiShell.Models;

namespace MultiShell.Services;

/// <summary>
/// Implements deterministic folder color coding for tabs, displaying all folder levels
/// with Padovan width multipliers without dynamic cross-tab omission (REQ-TAB-025).
/// </summary>
public sealed class PathColorCodingService : IPathColorCodingService
{
    /// <summary>
    /// Maximum number of color stripe elements displayed on a tab to prevent visual clutter.
    /// </summary>
    public const int MaxVisibleStripes = 16;

    /// <summary>
    /// Base height of each color stripe in pixels.
    /// </summary>
    public const double BaseStripeHeight = 3.0;

    /// <summary>
    /// Base width of the initial color stripe in pixels.
    /// </summary>
    public const double BaseStripeWidth = 9.0;

    /// <summary>
    /// Distinct strictly increasing multipliers from the Padovan sequence: 1, 2, 3, 4, 5, 7, 9, 12, 16, 21, 28, 37.
    /// </summary>
    public static readonly double[] PadovanMultipliers = [1, 2, 3, 4, 5, 7, 9, 12, 16, 21, 28, 37];

    /// <summary>
    /// Gets the Padovan multiplier for the given 0-based depth level.
    /// </summary>
    public static double GetPadovanMultiplier(int index)
    {
        if (index <= 0) return PadovanMultipliers[0];
        return index < PadovanMultipliers.Length ? PadovanMultipliers[index] : PadovanMultipliers[^1];
    }

    private const double GoldenRatioConjugate = 0.618033988749895;

    /// <inheritdoc />
    public IReadOnlyList<PathColorStripe> GetStripesForPath(string? path, IEnumerable<string?>? allPaths = null)
    {
        var targetSegments = GetPathSegments(path);
        if (targetSegments.Count == 0)
        {
            return Array.Empty<PathColorStripe>();
        }

        // Always display all folder levels. Filter out drive letters (e.g. "C:") and relative tokens ("." / "..").
        var folders = targetSegments
            .Where(seg => !seg.EndsWith(':') && seg != "." && seg != ".." && !string.IsNullOrWhiteSpace(seg))
            .Take(MaxVisibleStripes)
            .ToList();

        if (folders.Count == 0)
        {
            return Array.Empty<PathColorStripe>();
        }

        var stripes = new List<PathColorStripe>(folders.Count);
        for (int i = 0; i < folders.Count; i++)
        {
            var folder = folders[i];
            var color = GetColorForFolderName(folder);
            string hexColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            double width = BaseStripeWidth * GetPadovanMultiplier(i);
            stripes.Add(new PathColorStripe(folder, hexColor, width, BaseStripeHeight));
        }

        return stripes;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetPathSegments(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return Array.Empty<string>();
        }

        var trimmed = path.Trim();
        var parts = trimmed.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
        return parts;
    }

    /// <inheritdoc />
    public int GetCommonPrefixLength(IReadOnlyList<IReadOnlyList<string>> allSegments)
    {
        if (allSegments == null || allSegments.Count <= 1)
        {
            return 0;
        }

        int minLen = allSegments.Min(s => s.Count);
        if (minLen == 0)
        {
            return 0;
        }

        int common = 0;
        for (int i = 0; i < minLen; i++)
        {
            string first = allSegments[0][i];
            bool allMatch = true;
            for (int j = 1; j < allSegments.Count; j++)
            {
                if (!string.Equals(first, allSegments[j][i], StringComparison.OrdinalIgnoreCase))
                {
                    allMatch = false;
                    break;
                }
            }

            if (allMatch)
            {
                common++;
            }
            else
            {
                break;
            }
        }

        return common;
    }

    /// <inheritdoc />
    public Color GetColorForFolderName(string folderName)
    {
        if (string.IsNullOrWhiteSpace(folderName))
        {
            return Color.FromRgb(128, 128, 128);
        }

        uint hash = ComputeFnv1aHash(folderName.Trim());

        // Knuth's multiplicative hashing to scatter hashes evenly across the 360-degree color wheel
        float hue = (float)((hash * GoldenRatioConjugate) % 1.0) * 360f;
        if (hue < 0)
        {
            hue += 360f;
        }

        // Saturation 68%, Lightness 58% for high readability and vibrant aesthetics
        return HslToRgb(hue, 0.68f, 0.58f);
    }

    /// <summary>
    /// Computes a 32-bit FNV-1a hash case-insensitively, guaranteeing cross-run and cross-platform stability.
    /// </summary>
    public static uint ComputeFnv1aHash(string text)
    {
        uint hash = 2166136261;
        foreach (char c in text)
        {
            hash ^= char.ToUpperInvariant(c);
            hash *= 16777619;
        }
        return hash;
    }

    /// <summary>
    /// Converts HSL (Hue 0-360, Saturation 0-1, Lightness 0-1) to an Avalonia RGB Color.
    /// </summary>
    public static Color HslToRgb(float h, float s, float l)
    {
        float c = (1f - Math.Abs(2f * l - 1f)) * s;
        float x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
        float m = l - c / 2f;

        float r = 0, g = 0, b = 0;
        if (h < 60f) { r = c; g = x; b = 0; }
        else if (h < 120f) { r = x; g = c; b = 0; }
        else if (h < 180f) { r = 0; g = c; b = x; }
        else if (h < 240f) { r = 0; g = x; b = c; }
        else if (h < 300f) { r = x; g = 0; b = c; }
        else { r = c; g = 0; b = x; }

        byte red = (byte)Math.Clamp((int)Math.Round((r + m) * 255f), 0, 255);
        byte green = (byte)Math.Clamp((int)Math.Round((g + m) * 255f), 0, 255);
        byte blue = (byte)Math.Clamp((int)Math.Round((b + m) * 255f), 0, 255);

        return Color.FromRgb(red, green, blue);
    }
}
