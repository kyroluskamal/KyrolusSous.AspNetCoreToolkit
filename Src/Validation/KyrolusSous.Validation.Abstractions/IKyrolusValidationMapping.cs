namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Defines a contract for transforming or standardizing validation error codes before returning them to clients.
/// </summary>
/// <example>
/// <code>
/// public class ApiErrorCodeMapper : IKyrolusValidationErrorCodeMapper
/// {
///     public string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context)
///     {
///         // Standardize error codes to uppercase with prefix
///         return string.IsNullOrWhiteSpace(failure.ErrorCode) 
///             ? "ERR_GENERIC_VALIDATION" 
///             : $"API_{failure.ErrorCode.ToUpperInvariant()}";
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationErrorCodeMapper
{
    /// <summary>
    /// Maps or transforms the raw error code into a standardized client-facing error code.
    /// </summary>
    /// <param name="failure">The original validation failure.</param>
    /// <param name="context">The validation context.</param>
    /// <returns>The mapped error code string, or null/empty to keep original.</returns>
    string? MapErrorCode(KyrolusValidationFailure failure, KyrolusValidationContext context);
}

/// <summary>
/// Defines a contract for transforming field/property paths (e.g., converting PascalCase to camelCase or json-pointer format).
/// </summary>
/// <example>
/// <code>
/// public class CamelCaseFieldPathMapper : IKyrolusValidationFieldPathMapper
/// {
///     public string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context)
///     {
///         if (string.IsNullOrEmpty(failure.PropertyName)) return failure.PropertyName;
///         return char.ToLowerInvariant(failure.PropertyName[0]) + failure.PropertyName[1..];
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusValidationFieldPathMapper
{
    /// <summary>
    /// Maps or transforms the property name or field path into the desired client path format.
    /// </summary>
    /// <param name="failure">The original validation failure.</param>
    /// <param name="context">The validation context.</param>
    /// <returns>The mapped field path string.</returns>
    string? MapFieldPath(KyrolusValidationFailure failure, KyrolusValidationContext context);
}
