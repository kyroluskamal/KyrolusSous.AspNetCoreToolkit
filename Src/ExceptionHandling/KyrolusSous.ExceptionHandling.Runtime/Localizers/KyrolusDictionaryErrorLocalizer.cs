namespace KyrolusSous.ExceptionHandling.Runtime.Localizers;

public sealed class KyrolusDictionaryErrorLocalizer(IReadOnlyDictionary<string, string> translations)
    : IKyrolusErrorLocalizer
{
    private readonly IReadOnlyDictionary<string, string> translations = translations;

    public string? Localize(string code, string? defaultMessage, CultureInfo? culture)
    {
        if (string.IsNullOrWhiteSpace(code))
            return defaultMessage;

        return translations.TryGetValue(code, out var value) ? value : defaultMessage;
    }
}
