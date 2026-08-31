namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Represents the result of a localization lookup.
/// </summary>
/// <param name="Value">The localized message string or fallback value.</param>
/// <param name="ResourceNotFound">Indicates whether the translation key was found in the localization store.</param>
/// <param name="SearchedLocation">Optional info on the culture or file path searched.</param>
public readonly record struct KyrolusLocalizationResult(
    string Value,
    bool ResourceNotFound = false,
    string? SearchedLocation = null)
{
    public static implicit operator string(KyrolusLocalizationResult result) => result.Value;
}
