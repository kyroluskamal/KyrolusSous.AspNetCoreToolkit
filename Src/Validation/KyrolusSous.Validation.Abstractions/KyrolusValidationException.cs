namespace KyrolusSous.Validation.Abstractions;

/// <summary>
/// Exception thrown when one or more validation rules fail during execution.
/// </summary>
/// <example>
/// <code>
/// var failures = await engine.ValidateAsync(request);
/// if (failures.Count > 0)
/// {
///     throw new KyrolusValidationException(failures);
/// }
/// </code>
/// </example>
public sealed class KyrolusValidationException : Exception
{
    /// <summary>
    /// Gets the list of validation failures associated with this exception.
    /// </summary>
    public IReadOnlyList<KyrolusValidationFailure> Errors { get; }

    /// <summary>
    /// Initializes a new instance with a collection of validation failures.
    /// </summary>
    public KyrolusValidationException(IReadOnlyList<KyrolusValidationFailure> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors ?? [];
    }

    /// <summary>
    /// Initializes a new instance with a custom message and validation failures.
    /// </summary>
    public KyrolusValidationException(string message, IReadOnlyList<KyrolusValidationFailure> errors)
        : base(message)
    {
        Errors = errors ?? [];
    }

    /// <summary>
    /// Initializes a new instance with an enumerable of validation failures.
    /// </summary>
    public KyrolusValidationException(IEnumerable<KyrolusValidationFailure> errors)
        : this(errors?.ToList() ?? [])
    {
    }

    /// <summary>
    /// Initializes a new instance with a custom message and enumerable of validation failures.
    /// </summary>
    public KyrolusValidationException(string message, IEnumerable<KyrolusValidationFailure> errors)
        : this(message, errors?.ToList() ?? [])
    {
    }

    private static string BuildMessage(IReadOnlyList<KyrolusValidationFailure>? errors)
    {
        if (errors is null || errors.Count == 0)
        {
            return "Validation failed.";
        }

        var details = string.Join("; ", errors.Select(e =>
            string.IsNullOrWhiteSpace(e.PropertyName)
                ? e.ErrorMessage
                : $"{e.PropertyName}: {e.ErrorMessage}"));

        return $"Validation failed for {errors.Count} rule(s): {details}";
    }
}
