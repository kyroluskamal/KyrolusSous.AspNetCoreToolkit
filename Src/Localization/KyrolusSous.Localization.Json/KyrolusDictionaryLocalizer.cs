namespace KyrolusSous.Localization.Json;

/// <summary>
/// In-memory dictionary-based localizer supporting per-culture map lookup, hierarchical culture fallback
/// (matching <see cref="KyrolusJsonLocalizer"/>'s behavior), and parameter interpolation.
/// </summary>
/// <param name="cultureMaps">Per-culture translation maps, keyed by culture name (e.g. "ar-EG", "ar", "en").</param>
/// <param name="invariantMap">Optional invariant (culture-agnostic) fallback map, tried after every culture candidate is exhausted.</param>
/// <param name="fallbackCulture">
/// Optional culture name (e.g. "en") to try first - after the requested culture's own hierarchy is exhausted -
/// before <paramref name="fallbackCultures"/> and then <paramref name="invariantMap"/>. An unrecognized culture
/// name is ignored rather than throwing.
/// </param>
/// <param name="fallbackCultures">
/// Additional culture names to try, in order, after <paramref name="fallbackCulture"/> - for more than one
/// fallback level (e.g. a regional dialect, then a secondary administrative language, then invariant).
/// </param>
public sealed class KyrolusDictionaryLocalizer(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> cultureMaps,
    IReadOnlyDictionary<string, string>? invariantMap = null,
    string? fallbackCulture = null,
    IEnumerable<string>? fallbackCultures = null) : IKyrolusLocalizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _cultureMaps = cultureMaps
        ?? throw new ArgumentNullException(nameof(cultureMaps));
    private readonly IReadOnlyDictionary<string, string>? _invariantMap = invariantMap;
    private readonly string[] _fallbackCultures = BuildFallbackChain(fallbackCulture, fallbackCultures);

    private static string[] BuildFallbackChain(string? single, IEnumerable<string>? many)
    {
        var chain = new List<string>();
        if (!string.IsNullOrWhiteSpace(single)) chain.Add(single);
        if (many is not null) chain.AddRange(many);
        return [.. chain];
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new KyrolusLocalizationResult(string.Empty, ResourceNotFound: true);

        var targetCulture = culture ?? CultureInfo.CurrentUICulture;

        foreach (var cultureCandidate in KyrolusCultureFallbackResolver.GetCandidates(targetCulture, _fallbackCultures))
            if (_cultureMaps.TryGetValue(cultureCandidate, out var map) && map.TryGetValue(key, out var localized))
                return new KyrolusLocalizationResult(localized, ResourceNotFound: false, SearchedLocation: cultureCandidate);

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

    /// <inheritdoc />
    public IEnumerable<string> GetAllKeys(CultureInfo? culture = null)
    {
        var targetCulture = culture ?? CultureInfo.CurrentUICulture;
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cultureCandidate in KyrolusCultureFallbackResolver.GetCandidates(targetCulture, _fallbackCultures))
            if (_cultureMaps.TryGetValue(cultureCandidate, out var map))
                foreach (var key in map.Keys)
                    keys.Add(key);

        if (_invariantMap is not null)
            foreach (var key in _invariantMap.Keys)
                keys.Add(key);

        return keys;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableCultures() => _cultureMaps.Keys;
}
