namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusStringLocalizerErrorLocalizer(IStringLocalizer localizer)
    : IKyrolusErrorLocalizer
{
    private readonly IStringLocalizer localizer = localizer;

    public string? Localize(string code, string? defaultMessage, CultureInfo? culture)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return defaultMessage;
        }

        var originalCulture = CultureInfo.CurrentUICulture;
        if (culture is not null)
        {
            CultureInfo.CurrentUICulture = culture;
        }

        var value = localizer[code];
        if (culture is not null)
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }

        return value.ResourceNotFound ? defaultMessage : value.Value;
    }
}
