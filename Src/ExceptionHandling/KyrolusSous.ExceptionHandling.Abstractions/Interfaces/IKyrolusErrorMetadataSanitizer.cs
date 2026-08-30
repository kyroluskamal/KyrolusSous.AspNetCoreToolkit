namespace KyrolusSous.ExceptionHandling.Abstractions.Interfaces;

/// <summary>
/// Defines the contract for sanitizing diagnostic error metadata before returning it in HTTP responses.
/// </summary>
/// <remarks>
/// Filters out sensitive keys (e.g. passwords, tokens, API keys, connection strings) to prevent accidental data leaks.
/// </remarks>
public interface IKyrolusErrorMetadataSanitizer
{
    /// <summary>
    /// Filters and removes sensitive entries from the raw metadata dictionary.
    /// </summary>
    /// <param name="metadata">The original raw metadata dictionary.</param>
    /// <param name="context">The ambient request context.</param>
    /// <returns>A sanitized dictionary safe for serialization to the client.</returns>
    IReadOnlyDictionary<string, object?> Sanitize(IReadOnlyDictionary<string, object?> metadata, KyrolusErrorContext context);
}
