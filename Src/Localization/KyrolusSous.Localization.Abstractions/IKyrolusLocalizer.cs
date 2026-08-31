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
    /// <param name="arguments">
    /// An <c>object?[]</c> for positional placeholders (e.g. {0}), or an
    /// <see cref="IEnumerable{T}"/> of <see cref="KeyValuePair{TKey, TValue}"/> / <see cref="IDictionary{TKey, TValue}"/>
    /// (e.g. <c>IDictionary&lt;string, object?&gt;</c>) for named placeholders (e.g. {PropertyName}). No reflection is
    /// performed on the argument, so arbitrary POCOs/anonymous objects are not supported - keeps this AOT/trim safe.
    /// </param>
    /// <param name="culture">Optional explicit culture; defaults to <see cref="CultureInfo.CurrentUICulture"/> when null.</param>
    /// <returns>A <see cref="KyrolusLocalizationResult"/> containing the formatted localized string.</returns>
    KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null);

    /// <summary>
    /// Formats an arbitrary template string with the provided arguments.
    /// </summary>
    /// <param name="template">The template string containing placeholders (e.g. "Value {AttemptedValue} for {PropertyName} is invalid").</param>
    /// <param name="arguments">
    /// An <c>object?[]</c> for positional placeholders, or a key-value dictionary/sequence for named placeholders.
    /// See <see cref="GetString(string, object?, CultureInfo?)"/> for the full list of supported shapes.
    /// </param>
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
