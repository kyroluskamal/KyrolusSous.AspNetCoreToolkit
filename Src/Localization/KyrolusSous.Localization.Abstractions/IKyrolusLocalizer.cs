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
    /// <returns>
    /// The formatted string, with each placeholder value substituted verbatim (<c>ToString()</c>) and
    /// <b>no HTML/output encoding applied</b> - the same division of responsibility as <see cref="string.Format(string, object?)"/>,
    /// where the rendering layer (a Razor view, an email template engine, etc.) is expected to encode at output
    /// time. Be careful if any argument value can come from user input (e.g. a validation failure's
    /// <c>AttemptedValue</c>) and the result is ever written into a context that skips auto-encoding (raw HTML,
    /// <c>Html.Raw</c>, a frontend inserting into <c>innerHTML</c>) - that combination is an XSS risk the same
    /// way it would be for any other unencoded string concatenation.
    /// </returns>
    string Format(string template, object? arguments);

    /// <summary>
    /// Resolves a pluralized translation: looks up <c>"{key}.{category}"</c> for the CLDR plural category of
    /// <paramref name="count"/> under <paramref name="culture"/> (e.g. <c>"Items.one"</c>, <c>"Items.other"</c>;
    /// see <see cref="KyrolusPluralRules"/>), falling back to <c>"{key}.other"</c> and then the plain
    /// <paramref name="key"/> if no category-specific variant is found. <paramref name="count"/> is injected
    /// into <paramref name="arguments"/> under a <c>"count"</c> key (unless already present) so a template can
    /// reference <c>{count}</c> directly - this injection only applies to named (dictionary/<see cref="KeyValuePair{TKey, TValue}"/>)
    /// arguments; positional (<c>object?[]</c>) arguments are passed through unchanged.
    /// </summary>
    /// <param name="key">The base translation key, without a category suffix.</param>
    /// <param name="count">The count to pluralize for.</param>
    /// <param name="arguments">Additional template arguments; see <see cref="GetString(string, object?, CultureInfo?)"/> for supported shapes.</param>
    /// <param name="culture">Optional explicit culture; defaults to <see cref="CultureInfo.CurrentUICulture"/> when null.</param>
    /// <example>
    /// <code>
    /// // Items.zero = "No items", Items.one = "{count} item", Items.other = "{count} items"
    /// // (Items.two/.few/.many also defined for a language like Arabic that distinguishes them)
    /// localizer.GetPlural("Items", cart.Count).Value;
    /// </code>
    /// </example>
    KyrolusLocalizationResult GetPlural(string key, long count, object? arguments = null, CultureInfo? culture = null)
    {
        var resolvedCulture = culture ?? CultureInfo.CurrentUICulture;
        var category = KyrolusPluralRules.Resolve(resolvedCulture, count);

        var result = GetString($"{key}.{category.ToString().ToLowerInvariant()}", resolvedCulture);

        if (result.ResourceNotFound && category != KyrolusPluralCategory.Other)
            result = GetString($"{key}.other", resolvedCulture);

        if (result.ResourceNotFound)
            result = GetString(key, resolvedCulture);

        if (result.ResourceNotFound || string.IsNullOrEmpty(result.Value))
            return result;

        return result with { Value = Format(result.Value, MergeCountArgument(arguments, count)) };
    }

    /// <summary>
    /// Enumerates every translation key known for <paramref name="culture"/> (or <see cref="CultureInfo.CurrentUICulture"/>
    /// when <see langword="null"/>), including whatever the implementation's own fallback chain would pull in
    /// for that culture - useful for tooling that audits translation completeness (e.g. diffing "en"'s keys
    /// against "ar"'s to find what's missing there). The default implementation returns an empty sequence,
    /// since this base interface has no general notion of "every key" for an arbitrary lookup source; concrete
    /// localizers that actually hold a full key set (JSON-backed, dictionary-backed, composite, or an
    /// <c>IStringLocalizer</c> adapter) override it.
    /// </summary>
    IEnumerable<string> GetAllKeys(CultureInfo? culture = null) => [];

    /// <summary>
    /// Enumerates every culture name this localizer actually holds translations for (e.g. <c>["ar-EG", "ar", "en"]</c>) -
    /// useful for building a language switcher or validating a requested culture is actually supported before
    /// offering it to a user. The default implementation returns an empty sequence; concrete localizers that
    /// hold a real per-culture data set (JSON-backed, dictionary-backed, composite) override it. An
    /// <c>IStringLocalizer</c> adapter has no general way to enumerate this from the wrapped interface, so it
    /// also returns empty.
    /// </summary>
    IEnumerable<string> GetAvailableCultures() => [];

    /// <summary>Merges <paramref name="count"/> into named arguments under a "count" key; positional arguments pass through untouched.</summary>
    private static object? MergeCountArgument(object? arguments, long count)
    {
        Dictionary<string, object?> merged;
        switch (arguments)
        {
            case null:
                merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                break;
            case IEnumerable<KeyValuePair<string, object?>> namedArgs:
                merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in namedArgs) merged[k] = v;
                break;
            case IEnumerable<KeyValuePair<string, string>> namedArgs:
                merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                foreach (var (k, v) in namedArgs) merged[k] = v;
                break;
            default:
                return arguments; // positional (object?[]) or an unsupported shape: leave as-is
        }

        merged.TryAdd("count", count);
        return merged;
    }
}

/// <summary>
/// Strongly-typed localizer for a specific category or marker type.
/// </summary>
/// <typeparam name="TCategory">Marker type identifying the category or domain scope.</typeparam>
public interface IKyrolusLocalizer<TCategory> : IKyrolusLocalizer
{
}
