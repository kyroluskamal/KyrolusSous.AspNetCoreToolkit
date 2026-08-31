namespace KyrolusSous.Localization.Json;

/// <summary>
/// In-memory dictionary-based localizer supporting per-culture map lookup and parameter interpolation.
/// </summary>
public sealed class KyrolusDictionaryLocalizer(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> cultureMaps,
    IReadOnlyDictionary<string, string>? invariantMap = null) : IKyrolusLocalizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _cultureMaps = cultureMaps
        ?? throw new ArgumentNullException(nameof(cultureMaps));
    private readonly IReadOnlyDictionary<string, string>? _invariantMap = invariantMap;

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new KyrolusLocalizationResult(string.Empty, ResourceNotFound: true);

        var cultureName = (culture ?? CultureInfo.CurrentUICulture).Name;

        if (_cultureMaps.TryGetValue(cultureName, out var map) && map.TryGetValue(key, out var localized))
            return new KyrolusLocalizationResult(localized, ResourceNotFound: false, SearchedLocation: cultureName);

        if (_invariantMap is not null && _invariantMap.TryGetValue(key, out var fallback))
            return new KyrolusLocalizationResult(fallback, ResourceNotFound: false, SearchedLocation: "Invariant");

        return new KyrolusLocalizationResult(key, ResourceNotFound: true);
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
    {
        var result = GetString(key, culture);
        if (arguments is null || string.IsNullOrEmpty(result.Value))
            return result;

        var formatted = Format(result.Value, arguments);
        return new KyrolusLocalizationResult(formatted, result.ResourceNotFound, result.SearchedLocation);
    }

    /// <inheritdoc />
    public string Format(string template, object? arguments) => KyrolusLocalizationFormatter.Format(template, arguments);
}
