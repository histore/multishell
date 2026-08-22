namespace MultiShell.Models;

/// <summary>
/// Represents a supported user interface language option.
/// </summary>
/// <param name="Code">ISO 639-1 language code (e.g. "de", "en", "fr", "es").</param>
/// <param name="NativeName">The native display name (e.g. "Deutsch", "Français").</param>
/// <param name="EnglishName">The English display name (e.g. "German", "French").</param>
public record LanguageOption(string Code, string NativeName, string EnglishName);