namespace KyrolusSous.Localization.StringLocalizer;

/// <summary>
/// Bridges ASP.NET Core's built-in <see cref="IStringLocalizer"/> (resource files, satellite
/// assemblies, etc.) into <see cref="IKyrolusLocalizer"/>, so existing resource-based localization
/// can be reused as-is by anything that depends on <see cref="IKyrolusLocalizer"/>.
/// </summary>
public class KyrolusStringLocalizerAdapter(IStringLocalizer localizer) : IKyrolusLocalizer
{
    public KyrolusLocalizationResult GetString(string key, CultureInfo? culture = null)
    {
        if (string.IsNullOrWhiteSpace(key))
            return new KyrolusLocalizationResult(string.Empty, ResourceNotFound: true);

        var originalCulture = CultureInfo.CurrentUICulture;
        if (culture is not null)
            CultureInfo.CurrentUICulture = culture;

        try
        {
            var value = localizer[key];
            return new KyrolusLocalizationResult(value.Value, value.ResourceNotFound, culture?.Name);
        }
        finally
        {
            if (culture is not null)
                CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    public KyrolusLocalizationResult GetString(string key, object? arguments, CultureInfo? culture = null)
    {
        var result = GetString(key, culture);
        if (arguments is null || result.ResourceNotFound)
            return result;

        return result with { Value = Format(result.Value, arguments) };
    }

    public string Format(string template, object? arguments) => KyrolusLocalizationFormatter.Format(template, arguments);

    /// <inheritdoc />
    public IEnumerable<string> GetAllKeys(CultureInfo? culture = null)
    {
        var originalCulture = CultureInfo.CurrentUICulture;
        if (culture is not null)
            CultureInfo.CurrentUICulture = culture;

        try
        {
            // Materialized eagerly, inside the try, before the culture is restored in `finally` - GetAllStrings
            // returns a lazily-evaluated sequence, so returning it unmaterialized would let the actual
            // enumeration (and therefore the underlying resource lookup) happen after CurrentUICulture had
            // already been put back, silently using the wrong culture.
            return localizer.GetAllStrings(includeParentCultures: false).Select(s => s.Name).ToArray();
        }
        finally
        {
            if (culture is not null)
                CultureInfo.CurrentUICulture = originalCulture;
        }
    }
}

/// <summary>Strongly-typed <see cref="KyrolusStringLocalizerAdapter"/> for a specific resource category.</summary>
public sealed class KyrolusStringLocalizerAdapter<TCategory>(IStringLocalizer<TCategory> localizer)
    : KyrolusStringLocalizerAdapter(localizer), IKyrolusLocalizer<TCategory>
{
}
