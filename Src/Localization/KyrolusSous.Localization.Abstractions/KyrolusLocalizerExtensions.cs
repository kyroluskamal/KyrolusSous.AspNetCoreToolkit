
namespace KyrolusSous.Localization.Abstractions;

/// <summary>
/// Generic, domain-agnostic convenience methods for <see cref="IKyrolusLocalizer"/>: resolve a key
/// and fall back to a caller-supplied default when the localizer is unset or has no translation.
/// </summary>
public static class KyrolusLocalizerExtensions
{
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static string? GetStringOrDefault(
        this IKyrolusLocalizer? localizer,
        string key,
        string? defaultValue,
        CultureInfo? culture = null)
    {
        if (localizer is null) return defaultValue;

        var result = localizer.GetString(key, culture);
        return result.ResourceNotFound ? defaultValue : result.Value;
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static string? GetStringOrDefault(
        this IKyrolusLocalizer? localizer,
        string key,
        object? arguments,
        string? defaultValue,
        CultureInfo? culture = null)
    {
        if (localizer is null)return defaultValue;

        var result = localizer.GetString(key, arguments, culture);
        return result.ResourceNotFound ? defaultValue : result.Value;
    }

    /// <summary>Resolves a pluralized translation via <see cref="IKyrolusLocalizer.GetPlural"/>, falling back to <paramref name="defaultValue"/> when the localizer is unset or has no translation.</summary>
    [return: NotNullIfNotNull(nameof(defaultValue))]
    public static string? GetPluralOrDefault(
        this IKyrolusLocalizer? localizer,
        string key,
        long count,
        string? defaultValue,
        object? arguments = null,
        CultureInfo? culture = null)
    {
        if (localizer is null) return defaultValue;

        var result = localizer.GetPlural(key, count, arguments, culture);
        return result.ResourceNotFound ? defaultValue : result.Value;
    }
}
