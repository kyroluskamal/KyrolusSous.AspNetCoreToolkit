namespace KyrolusSous.Validation.Abstractions;

public sealed class KyrolusValidationException : Exception
{
    public KyrolusValidationException(IReadOnlyList<KyrolusValidationFailure> errors)
        : base("Validation failed.")
    {
        Errors = errors;
    }

    public IReadOnlyList<KyrolusValidationFailure> Errors { get; }
}
