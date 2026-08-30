namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

/// <summary>
/// Implemented by exceptions that provide a structured dictionary of custom key-value diagnostic metadata.
/// </summary>
public interface IKyrolusExceptionWithMetadata
{
    /// <summary>
    /// Gets the custom diagnostic metadata associated with this exception.
    /// </summary>
    /// <returns>A read-only dictionary of metadata key-value pairs.</returns>
    IReadOnlyDictionary<string, object?> GetMetadata();
}
