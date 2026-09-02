namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Shared culture-hierarchy fallback-chain logic used by every <see cref="IKyrolusLocalizer"/> implementation
/// (JSON-backed, in-memory dictionary-backed, etc.), so a lookup for e.g. "ar-EG" falls back through "ar" (and
/// an optional configured fallback culture) the same way no matter which implementation is doing the lookup -
/// keeping the promise made by <see cref="IKyrolusLocalizer"/>'s own contract ("supporting culture hierarchies")
/// consistent across every concrete localizer instead of each one reimplementing (and potentially diverging on)
/// the walk.
/// </summary>
public static class KyrolusCultureFallbackResolver
{
    /// <summary>
    /// Enumerates the candidate culture keys to try, in priority order: <paramref name="culture"/>'s own name,
    /// its parent's name, its root two-letter language code, then - for each name in <paramref name="fallbackCultureNames"/>,
    /// in the order given - that culture's own hierarchy, and finally the invariant key (<see cref="string.Empty"/>).
    /// Each distinct candidate is yielded at most once (case-insensitively), so callers can look each one up in
    /// turn without worrying about redundant repeat lookups.
    /// </summary>
    /// <param name="culture">The requested culture to resolve a fallback chain for.</param>
    /// <param name="fallbackCultureNames">
    /// Additional culture names (e.g. <c>["ar", "en"]</c>) to try, in order - after <paramref name="culture"/>'s
    /// own hierarchy is exhausted - before giving up to the invariant key. Supports more than one fallback
    /// level (e.g. a regional dialect, then a secondary administrative language, then invariant). An unset,
    /// empty, or unrecognized name is ignored.
    /// </param>
    public static IEnumerable<string> GetCandidates(CultureInfo culture, IEnumerable<string>? fallbackCultureNames = null)
    {
        ArgumentNullException.ThrowIfNull(culture);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in EnumerateHierarchy(culture))
            if (seen.Add(candidate))
                yield return candidate;

        if (fallbackCultureNames is not null)
            foreach (var fallbackCultureName in fallbackCultureNames)
                if (!string.IsNullOrWhiteSpace(fallbackCultureName) && TryResolveCulture(fallbackCultureName, out var fallbackCulture))
                    foreach (var candidate in EnumerateHierarchy(fallbackCulture))
                        if (seen.Add(candidate))
                            yield return candidate;

        if (seen.Add(string.Empty))
            yield return string.Empty;
    }

    /// <summary>Walks one culture's own name, parent name, and root language code - without the invariant key or any configured fallback.</summary>
    private static IEnumerable<string> EnumerateHierarchy(CultureInfo culture)
    {
        if (!string.IsNullOrWhiteSpace(culture.Name))
            yield return culture.Name;

        var parentName = culture.Parent?.Name;
        if (!string.IsNullOrWhiteSpace(parentName))
            yield return parentName;

        if (!string.IsNullOrWhiteSpace(culture.TwoLetterISOLanguageName))
            yield return culture.TwoLetterISOLanguageName;
    }

    /// <summary>Resolves a culture name defensively - an invalid/unrecognized name (e.g. a typo in configuration) is ignored rather than throwing.</summary>
    private static bool TryResolveCulture(string name, out CultureInfo culture)
    {
        try
        {
            culture = CultureInfo.GetCultureInfo(name);
            return true;
        }
        catch (CultureNotFoundException)
        {
            culture = CultureInfo.InvariantCulture;
            return false;
        }
    }
}
