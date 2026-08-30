namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 401 (Unauthorized) exception thrown when authentication credentials are missing, invalid, or expired.
/// </summary>
/// <remarks>
/// Throw this when the caller must authenticate (e.g., provide a valid Bearer token or API key) to perform the request.
/// </remarks>
/// <example>
/// <code>
/// if (token.IsExpired)
///     throw new KyrolusUnauthorizedException("Your session has expired. Please log in again.");
/// </code>
/// </example>
/// <param name="detail">An optional explanation of the authentication issue.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusUnauthorizedException(string? detail = null, Exception? innerException = null) 
    : KyrolusException(HttpStatusCode.Unauthorized, KyrolusErrorCodes.Unauthorized, "Unauthorized", detail, null, null, false, false, innerException)
{
}
