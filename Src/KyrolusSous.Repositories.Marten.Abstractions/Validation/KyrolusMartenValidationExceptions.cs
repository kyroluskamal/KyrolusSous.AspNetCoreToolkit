namespace KyrolusSous.Repositories.Marten.Abstractions.Validation;

public class KyrolusMartenValidationException : Exception
{
    public KyrolusMartenValidationException(string message) : base(message) { }

    public KyrolusMartenValidationException(string message, Exception innerException) : base(message, innerException) { }
}

public sealed class KyrolusMartenAggregateValidationException(IReadOnlyList<Exception> errors) : KyrolusMartenValidationException("Validation failed.")
{

    public IReadOnlyList<Exception> Errors { get; } = errors;
}
