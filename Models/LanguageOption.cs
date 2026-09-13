namespace MultiShell.Models;

/// <summary>
/// Represents a supported user interface language option.
/// </summary>
/// <param name="Code">ISO 639-1 language code (e.g. "de", "en", "fr", "es", "it", "pt").</param>
/// <param name="NativeName">The native display name (e.g. "Deutsch", "Français", "Italiano").</param>
/// <param name="EnglishName">The English display name (e.g. "German", "French", "Italian").</param>
public record LanguageOption(string Code, string NativeName, string EnglishName)
{
    /// <summary>
    /// Gets the uppercase two-letter language code (e.g. "DE", "EN").
    /// </summary>
    public string UpperCode => Code.ToUpperInvariant();

    /// <summary>
    /// Gets a formatted display string including native and English names.
    /// </summary>
    public string DisplayText => $"{UpperCode} - {NativeName} ({EnglishName})";
}