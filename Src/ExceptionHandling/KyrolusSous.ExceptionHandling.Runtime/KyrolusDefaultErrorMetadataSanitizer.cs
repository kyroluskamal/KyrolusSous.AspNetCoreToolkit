namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusDefaultErrorMetadataSanitizer(IOptions<KyrolusExceptionHandlingOptions> options)
    : IKyrolusErrorMetadataSanitizer
{
    private readonly KyrolusExceptionHandlingOptions options = options?.Value ?? new KyrolusExceptionHandlingOptions();

    public IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?>? metadata, KyrolusErrorContext context)
    {
        if (metadata is null) return new Dictionary<string, object?>();
        if (metadata.Count == 0 || !options.SanitizeMetadata) return metadata;

        var allowList = options.MetadataAllowList;
        if (allowList is { Count: > 0 })
        {
            var filtered = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, value) in metadata)
                if (allowList.Contains(key)) filtered[key] = SanitizeValue(value);

            return filtered;
        }

        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata)
            if (!IsSensitiveKey(key))
                sanitized[key] = SanitizeValue(value);

        return sanitized;
    }

    /// <summary>
    /// Recurses into nested dictionary values (e.g. a complex object stashed in <see cref="Exception.Data"/>) and
    /// scrubs sensitive keys there too. Only dictionary-shaped values are inspected - arbitrary POCOs would need
    /// reflection to walk, which this toolkit avoids for AOT/trimming compatibility.
    /// </summary>
    private object? SanitizeValue(object? value)
    {
        if (value is IEnumerable<KeyValuePair<string, object?>> nested)
        {
            var result = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var (key, nestedValue) in nested)
                if (!IsSensitiveKey(key))
                    result[key] = SanitizeValue(nestedValue);

            return result;
        }

        return value;
    }

    private bool IsSensitiveKey(string key)
    {
        if (options.SensitiveMetadataKeys.Contains(key)) return true;

        foreach (var sensitiveKey in options.SensitiveMetadataKeys)
            if (key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase)) return true;

        return false;
    }
}
