namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusNullValidationErrorLocalizer : IKyrolusValidationErrorLocalizer
{
    public string Localize(KyrolusValidationFailure failure, CultureInfo? culture = null) => failure.ErrorMessage;
}

public sealed class KyrolusDictionaryValidationErrorLocalizer(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> cultureMaps,
    IReadOnlyDictionary<string, string>? invariantMap = null) : IKyrolusValidationErrorLocalizer
{
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> cultureMaps = cultureMaps
        ?? throw new ArgumentNullException(nameof(cultureMaps));
    private readonly IReadOnlyDictionary<string, string>? invariantMap = invariantMap;

    public string Localize(KyrolusValidationFailure failure, CultureInfo? culture = null)
    {
        var key = failure.MessageKey ?? failure.ErrorCode ?? failure.ErrorMessage;
        var cultureName = (culture ?? CultureInfo.CurrentUICulture).Name;

        if (cultureMaps.TryGetValue(cultureName, out var map) && map.TryGetValue(key, out var localized))
            return localized;

        if (invariantMap is not null && invariantMap.TryGetValue(key, out var fallback))
            return fallback;

        return failure.ErrorMessage;
    }
}
