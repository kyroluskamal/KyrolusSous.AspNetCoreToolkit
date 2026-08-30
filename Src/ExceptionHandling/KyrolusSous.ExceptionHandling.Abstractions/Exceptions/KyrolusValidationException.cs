namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 400 validation failure carrying a collection of individual field-level error items
/// with smart contextual detail generation.
/// </summary>
/// <remarks>
/// Used to return structured RFC 7807 validation problem details containing a list of invalid fields and their specific error messages.
/// </remarks>
/// <example>
/// <code>
/// var errors = new List&lt;KyrolusErrorItem&gt;
/// {
///     new("Email", "invalid_email", "Email format is not valid."),
///     new("Age", "min_age", "Age must be 18 or older.")
/// };
/// throw new KyrolusValidationException(errors);
/// </code>
/// </example>
public sealed class KyrolusValidationException : KyrolusException
{
    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusValidationException"/> with a list of field-level errors
    /// and optional title and detail overrides.
    /// </summary>
    /// <param name="errors">The collection of field-level validation errors.</param>
    /// <param name="title">An optional title summary (defaults to "Validation failed").</param>
    /// <param name="detail">An optional detailed explanation of the failure. When null, a smart dynamic detail is generated.</param>
    public KyrolusValidationException(IEnumerable<KyrolusErrorItem> errors, string? title = null, string? detail = null)
        : this(errors as IReadOnlyList<KyrolusErrorItem> ?? [.. errors], title, detail)
    {
    }

    private KyrolusValidationException(IReadOnlyList<KyrolusErrorItem> errorsList, string? title, string? detail)
        : base(
            HttpStatusCode.BadRequest,
            KyrolusErrorCodes.Validation,
            title ?? "Validation failed",
            detail ?? ResolveDefaultDetail(errorsList),
            errorsList,
            null,
            false,
            false)
    {
    }

    private static string ResolveDefaultDetail(IReadOnlyList<KyrolusErrorItem> errors)
    {
        return errors.Count switch
        {
            0 => "Validation failed.",
            1 => !string.IsNullOrWhiteSpace(errors[0].Field)
                ? $"Validation failed on '{errors[0].Field}': {errors[0].Message ?? "Invalid value."}"
                : errors[0].Message ?? "Validation failed.",
            _ => $"{errors.Count} validation errors occurred in fields: {string.Join(", ", errors.Select(e => e.Field).Where(f => !string.IsNullOrWhiteSpace(f)).Distinct())}."
        };
    }
}
