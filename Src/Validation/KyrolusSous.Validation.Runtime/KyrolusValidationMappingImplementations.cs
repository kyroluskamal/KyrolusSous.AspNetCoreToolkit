namespace KyrolusSous.Validation.Runtime;

public sealed class KyrolusDelegateValidationErrorCodeMapper(
    Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver)
    : IKyrolusValidationErrorCodeMapper
{
    private readonly Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver =
        resolver ?? throw new ArgumentNullException(nameof(resolver));

    public string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context)
        => resolver(failure, context);
}

public sealed class KyrolusDelegateValidationFieldPathMapper(
    Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver)
    : IKyrolusValidationFieldPathMapper
{
    private readonly Func<KyrolusValidationFailure, KyrolusValidationContext, string?> resolver =
        resolver ?? throw new ArgumentNullException(nameof(resolver));

    public string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context)
        => resolver(failure, context);
}

public sealed class KyrolusDictionaryValidationErrorCodeMapper(
    IReadOnlyDictionary<string, string> map)
    : IKyrolusValidationErrorCodeMapper
{
    private readonly IReadOnlyDictionary<string, string> map = map
        ?? throw new ArgumentNullException(nameof(map));

    public string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context)
    {
        foreach (var key in GetLookupKeys(failure))
            if (map.TryGetValue(key, out var mapped) && !string.IsNullOrWhiteSpace(mapped))
                return mapped;

        return null;
    }

    private static IEnumerable<string> GetLookupKeys(KyrolusValidationFailure failure)
    {
        if (!string.IsNullOrWhiteSpace(failure.ErrorCode)) yield return failure.ErrorCode;
        if (!string.IsNullOrWhiteSpace(failure.MessageKey)) yield return failure.MessageKey;
        if (!string.IsNullOrWhiteSpace(failure.PropertyName)) yield return failure.PropertyName;
    }
}

public sealed class KyrolusDictionaryValidationFieldPathMapper(
    IReadOnlyDictionary<string, string> map)
    : IKyrolusValidationFieldPathMapper
{
    private readonly IReadOnlyDictionary<string, string> map = map
        ?? throw new ArgumentNullException(nameof(map));

    public string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context)
    {
        if (string.IsNullOrWhiteSpace(failure.PropertyName)) return null;

        return map.TryGetValue(failure.PropertyName, out var mapped)
            ? mapped
            : null;
    }
}
