namespace KyrolusSous.Validation.Runtime;

/// <summary>
/// <see cref="IKyrolusValidationErrorCodeMapper"/> that forwards each failure to a supplied delegate - a shortcut
/// for a one-off error-code mapping rule without writing a dedicated class. Neither mapper is registered by
/// default; register one explicitly to have <see cref="KyrolusValidationEngine"/> apply it.
/// </summary>
/// <param name="resolver">
/// Computes the mapped error code for a failure, or returns <see langword="null"/>/blank to leave the failure's
/// original <see cref="KyrolusValidationFailure.ErrorCode"/> untouched.
/// </param>
public sealed class KyrolusDelegateValidationErrorCodeMapper(
    Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver)
    : IKyrolusValidationErrorCodeMapper
{
    private readonly Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver =
        resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc />
    public string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context)
        => resolver(failure, context);
}

/// <summary>
/// <see cref="IKyrolusValidationFieldPathMapper"/> that forwards each failure to a supplied delegate - a shortcut
/// for a one-off field-path mapping rule (e.g. PascalCase to camelCase) without writing a dedicated class.
/// </summary>
/// <param name="resolver">
/// Computes the mapped field path for a failure, or returns <see langword="null"/>/blank to leave the failure's
/// original <see cref="KyrolusValidationFailure.FieldPath"/> untouched.
/// </param>
public sealed class KyrolusDelegateValidationFieldPathMapper(
    Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver)
    : IKyrolusValidationFieldPathMapper
{
    private readonly Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver =
        resolver ?? throw new ArgumentNullException(nameof(resolver));

    /// <inheritdoc />
    public string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context)
        => resolver(failure, context);
}

/// <summary>
/// <see cref="IKyrolusValidationErrorCodeMapper"/> backed by a static lookup table, for standardizing error codes
/// (e.g. every failure's own code, message key, or property name rewritten to an API-facing constant) without
/// writing custom mapping logic.
/// </summary>
/// <param name="map">The lookup table from a failure's identifying key (see <see cref="GetLookupKeys"/>) to its mapped error code.</param>
public sealed class KyrolusDictionaryValidationErrorCodeMapper(
    IReadOnlyDictionary<string, string> map)
    : IKyrolusValidationErrorCodeMapper
{
    private readonly IReadOnlyDictionary<string, string> map = map
        ?? throw new ArgumentNullException(nameof(map));

    /// <summary>
    /// Looks up <paramref name="failure"/> in <see cref="map"/> by, in order, its <see cref="KyrolusValidationFailure.ErrorCode"/>,
    /// <see cref="KyrolusValidationFailure.MessageKey"/>, then <see cref="KyrolusValidationFailure.PropertyName"/> -
    /// returning the first non-blank match, or <see langword="null"/> if none of those keys are present in the map.
    /// </summary>
    public string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context)
    {
        foreach (var key in GetLookupKeys(failure))
            if (map.TryGetValue(key, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                return mapped;

        return null;
    }

    /// <summary>Yields <paramref name="failure"/>'s non-blank <see cref="KyrolusValidationFailure.ErrorCode"/>, <see cref="KyrolusValidationFailure.MessageKey"/>, and <see cref="KyrolusValidationFailure.PropertyName"/>, in that priority order.</summary>
    private static IEnumerable<string> GetLookupKeys(KyrolusValidationFailure failure)
    {
        if (!string.IsNullOrWhiteSpace(failure.ErrorCode)) yield return failure.ErrorCode;
        if (!string.IsNullOrWhiteSpace(failure.MessageKey)) yield return failure.MessageKey;
        if (!string.IsNullOrWhiteSpace(failure.PropertyName)) yield return failure.PropertyName;
    }
}

/// <summary>
/// <see cref="IKyrolusValidationFieldPathMapper"/> backed by a static lookup table keyed by
/// <see cref="KyrolusValidationFailure.PropertyName"/>, for a fixed property-to-client-path rename table (e.g.
/// exposing an internal property name under a different public API field name) without custom mapping logic.
/// </summary>
/// <param name="map">The lookup table from a failure's <see cref="KyrolusValidationFailure.PropertyName"/> to its client-facing field path.</param>
public sealed class KyrolusDictionaryValidationFieldPathMapper(
    IReadOnlyDictionary<string, string> map)
    : IKyrolusValidationFieldPathMapper
{
    private readonly IReadOnlyDictionary<string, string> map = map
        ?? throw new ArgumentNullException(nameof(map));

    /// <summary>Looks up <paramref name="failure"/>'s <see cref="KyrolusValidationFailure.PropertyName"/> in <see cref="map"/>; returns <see langword="null"/> when the property name is blank or absent from the map.</summary>
    public string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(failure.PropertyName)) return null;

        return map.TryGetValue(failure.PropertyName, out var mapped)
            ? mapped
            : null;
    }
}
