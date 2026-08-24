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
                if (allowList.Contains(key))
                    filtered[key] = value;

            return filtered;
        }

        var sanitized = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in metadata)
            if (!IsSensitiveKey(key))
                sanitized[key] = value;

        return sanitized;
    }

    private bool IsSensitiveKey(string key)
    {
        if (options.SensitiveMetadataKeys.Contains(key)) return true;

        foreach (var sensitiveKey in options.SensitiveMetadataKeys)
        {
            if (key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}
