namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

public interface IKyrolusErrorMetadataSanitizer
{
    IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?> metadata, KyrolusErrorContext context);
}
