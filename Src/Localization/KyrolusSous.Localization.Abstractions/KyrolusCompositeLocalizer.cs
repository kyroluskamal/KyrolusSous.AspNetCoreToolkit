namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Composes multiple <see cref="IKyrolusLocalizer"/> sources into one fallback chain: each source is tried in
/// order, and the first one that resolves the key (<see cref="KyrolusLocalizationResult.ResourceNotFound"/> is
/// <see langword="false"/>) wins. Useful for layering sources - e.g. a per-tenant JSON override localizer
/// first, then a shared default JSON localizer, then an ASP.NET Core resource-file adapter as a last resort.
/// </summary>
/// <example>
/// <code>
/// services.AddSingleton&lt;IKyrolusLocalizer&gt;(sp =&gt; new KyrolusCompositeLocalizer(
///     new KyrolusJsonLocalizer(tenantOverridesOptions),
///     new KyrolusJsonLocalizer(defaultOptions),
///     new KyrolusStringLocalizerAdapter(sp.GetRequiredService&lt;IStringLocalizer&lt;SharedResource&gt;&gt;())));
/// </code>
/// </example>
public sealed class KyrolusCompositeLocalizer : IKyrolusLocalizer
{
    private readonly IReadOnlyList<IKyrolusLocalizer> _localizers;

    /// <param name="localizers">The sources to try, in priority order. At least one must be provided.</param>
    public KyrolusCompositeLocalizer(params IKyrolusLocalizer[] localizers)
    {
        ArgumentNullException.ThrowIfNull(localizers);
        if (localizers.Length == 0)
            throw new ArgumentException("At least one localizer must be provided.", nameof(localizers));

        _localizers = localizers;
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
    {
        foreach (var localizer in _localizers)
        {
            var result = localizer.GetString(key, culture);
            if (!result.ResourceNotFound)
                return result;
        }

        return new KyrolusLocalizationResult(key, ResourceNotFound: true);
    }

    /// <inheritdoc />
    public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
    {
        foreach (var localizer in _localizers)
        {
            var result = localizer.GetString(key, arguments, culture);
            if (!result.ResourceNotFound)
                return result;
        }

        return new KyrolusLocalizationResult(key, ResourceNotFound: true);
    }

    /// <inheritdoc />
    public string Format(string template, object? arguments) => KyrolusLocalizationFormatter.Format(template, arguments);

    /// <inheritdoc />
    public IEnumerable<string> GetAllKeys(CultureInfo? culture = null)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var localizer in _localizers)
            foreach (var key in localizer.GetAllKeys(culture))
                keys.Add(key);

        return keys;
    }

    /// <inheritdoc />
    public IEnumerable<string> GetAvailableCultures()
    {
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var localizer in _localizers)
            foreach (var cultureName in localizer.GetAvailableCultures())
                cultures.Add(cultureName);

        return cultures;
    }
}
