namespace KyrolusSous.Validation.Abstractions;

public sealed class KyrolusValidationException : Exception
{
    public IReadOnlyList<KyrolusValidationFailure> Errors { get; }

    public KyrolusValidationException(IReadOnlyList<KyrolusValidationFailure> errors)
        : base(BuildMessage(errors))
    {
        Errors = errors ?? [];
    }

    public KyrolusValidationException(string message, IReadOnlyList<KyrolusValidationFailure> errors)
        : base(message)
    {
        Errors = errors ?? [];
    }

    public KyrolusValidationException(IEnumerable<KyrolusValidationFailure> errors)
        : this(errors?.ToList() ?? [])
    {
    }

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
