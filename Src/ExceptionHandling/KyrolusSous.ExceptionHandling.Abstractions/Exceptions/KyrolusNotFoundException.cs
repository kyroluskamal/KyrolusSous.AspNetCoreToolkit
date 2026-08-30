namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 404 (Not Found) exception thrown when a requested entity or resource cannot be found in the database/store.
/// </summary>
/// <remarks>
/// Automatically sets <see cref="HttpStatusCode.NotFound"/> and attaches the entity name and key as structured metadata
/// for client diagnostics and ProblemDetails responses.
/// </remarks>
/// <example>
/// <code>
/// var product = await repository.GetByIdAsync(productId);
/// if (product is null)
///     throw new KyrolusNotFoundException("Product", productId);
/// </code>
/// </example>
public sealed class KyrolusNotFoundException(string entityName, object key, Exception? innerException = null) 
: KyrolusException(
        HttpStatusCode.NotFound,
        KyrolusErrorCodes.NotFound,
        $"{entityName} not found",
        $"{entityName} with key '{key}' was not found.",
        null,
        new Dictionary<string, object?> { ["entityName"] = entityName, ["key"] = key?.ToString() },
        false,
        false,
        innerException)
{
    /// <summary>
    /// Gets the name of the missing entity (e.g. "User", "Order", "Product").
    /// </summary>
    public string? EntityName { get; } = entityName;

    /// <summary>
    /// Gets the identifier/key of the missing entity that was looked up.
    /// </summary>
    public string? Key { get; } = key?.ToString();

    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusNotFoundException"/> with a string key.
    /// </summary>
    /// <param name="entityName">The name of the entity.</param>
    /// <param name="key">The key or identifier.</param>
    /// <param name="innerException">An optional inner exception.</param>
    public KyrolusNotFoundException(string entityName, string key, Exception? innerException = null)
        : this(entityName, (object)key, innerException)
    {
    }
}
