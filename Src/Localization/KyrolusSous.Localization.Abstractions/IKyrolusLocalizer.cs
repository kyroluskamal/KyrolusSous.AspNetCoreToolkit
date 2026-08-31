using System.Globalization;

namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Core contract for resolving localized strings by key, supporting culture hierarchies, template interpolation, and parameter formatting.
/// </summary>
public interface IKyrolusLocalizer
{
    /// <summary>
    /// Gets the localized string for the specified key.
    /// </summary>
    /// <param name="key">The translation key identifier.</param>
    /// <param name="culture">Optional explicit culture; defaults to <see cref="CultureInfo.CurrentUICulture"/> when null.</param>
    /// <returns>A <see cref="KyrolusLocalizationResult"/> containing the translated string or fallback.</returns>
    KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null);

    /// <summary>
    /// Gets the localized string for the specified key with arguments for template placeholders.
    /// </summary>
    /// <param name="key">The translation key identifier.</param>
    /// <param name="arguments">Positional arguments or a key-value dictionary/object for placeholder substitution (e.g. {0} or {PropertyName}).</param>
    /// <param name="culture">Optional explicit culture; defaults to <see cref="CultureInfo.CurrentUICulture"/> when null.</param>
    /// <returns>A <see cref="KyrolusLocalizationResult"/> containing the formatted localized string.</returns>
    KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null);

    /// <summary>
    /// Formats an arbitrary template string with the provided arguments.
    /// </summary>
    /// <param name="template">The template string containing placeholders (e.g. "Value {AttemptedValue} for {PropertyName} is invalid").</param>
    /// <param name="arguments">Named values or key-value dictionary for substitution.</param>
    /// <returns>The formatted string.</returns>
    string Format(string template, object? arguments);
}

/// <summary>
/// Strongly-typed localizer for a specific category or marker type.
/// </summary>
/// <typeparam name="TCategory">Marker type identifying the category or domain scope.</typeparam>
public interface IKyrolusLocalizer<TCategory> : IKyrolusLocalizer
{
}
