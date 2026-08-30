namespace KyrolusSous.ExceptionHandling.Abstractions.Exceptions;

/// <summary>
/// Represents an HTTP 429 (Too Many Requests) exception thrown when client traffic exceeds configured rate limit quotas.
/// </summary>
/// <remarks>
/// Automatically marked with <c>IsTransient = true</c> indicating the client can retry after a cooldown period.
/// </remarks>
/// <example>
/// <code>
/// if (requestsCount > maxAllowedPerMinute)
///     throw new KyrolusRateLimitException("API rate limit exceeded. Please wait 60 seconds before making additional requests.");
/// </code>
/// </example>
/// <param name="detail">An optional explanation of the rate limit constraint.</param>
/// <param name="innerException">An optional inner exception.</param>
public sealed class KyrolusRateLimitException(string? detail = null, Exception? innerException = null) 
    : KyrolusException((HttpStatusCode)429, KyrolusErrorCodes.RateLimit, "Rate limit exceeded", detail, null, null, true, false, innerException)
{
}
