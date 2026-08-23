namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

public interface IKyrolusExceptionWithMetadata
{
    IReadOnlyDictionary<string, object?> GetMetadata();
}
