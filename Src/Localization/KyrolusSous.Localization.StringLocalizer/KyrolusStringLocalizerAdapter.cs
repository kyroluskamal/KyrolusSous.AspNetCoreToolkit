namespace KyrolusSous.Localization.StringLocalizer;

/// <summary>
/// Bridges ASP.NET Core's built-in <see cref="IStringLocalizer"/> (resource files, satellite
/// assemblies, etc.) into <see cref="IKyrolusLocalizer"/>, so existing resource-based localization
/// can be reused as-is by anything that depends on <see cref="IKyrolusLocalizer"/>.
/// </summary>
public sealed class KyrolusStringLocalizerAdapter(IStringLocalizer localizer) : IKyrolusLocalizer
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
}
