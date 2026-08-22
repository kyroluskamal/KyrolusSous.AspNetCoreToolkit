namespace KyrolusSous.ExceptionHandling.Runtime;

public sealed class KyrolusDefaultErrorMetadataSanitizer(IOptions<KyrolusExceptionHandlingOptions> options)
    : IKyrolusErrorMetadataSanitizer
{
    private readonly KyrolusExceptionHandlingOptions options = options.Value;

    public IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?> metadata, KyrolusErrorContext context)
    {
        if (!options.SanitizeMetadata || metadata.Count == 0) return metadata;

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
            if (!options.SensitiveMetadataKeys.Contains(key))
                sanitized[key] = value;

        return sanitized;
    }
}
