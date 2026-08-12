namespace KyrolusSous.Validation.Abstractions;

public sealed class KyrolusValidationException(IReadOnlyList<KyrolusValidationFailure> errors) : Exception("Validation failed.")
{
    public IReadOnlyList<KyrolusValidationFailure> Errors { get; } = errors;
}
