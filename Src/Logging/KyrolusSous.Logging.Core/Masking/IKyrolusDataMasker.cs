namespace KyrolusSous.Logging.Core.Masking;

/// <summary>
/// Service contract for sanitizing and masking sensitive data and PII before logging.
/// </summary>
public interface IKyrolusDataMasker
{
    /// <summary>
    /// Checks if a property or field name is considered sensitive by convention.
    /// </summary>
    bool IsSensitivePropertyName(string propertyName);

    /// <summary>
    /// Masks a plain string value according to the specified rule or default masking policy.
    /// </summary>
    string MaskString(string? value, KyrolusMaskedAttribute? rule = null);

    /// <summary>
    /// Sanitizes a dictionary of structured log properties, replacing sensitive values with masked equivalents.
    /// </summary>
    IReadOnlyDictionary<string, object?> SanitizeProperties(IReadOnlyDictionary<string, object?>? properties);
}
