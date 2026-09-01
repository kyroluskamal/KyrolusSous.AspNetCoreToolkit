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
///     throw new KyrolusRateLimitException(
///         "API rate limit exceeded. Please wait 60 seconds before making additional requests.",
///         retryAfter: TimeSpan.FromSeconds(60));
/// </code>
/// </example>
public sealed class KyrolusRateLimitException : KyrolusException
{
    /// <summary>
    /// Initializes a new instance of <see cref="KyrolusRateLimitException"/>.
    /// </summary>
    /// <param name="detail">An optional explanation of the rate limit constraint.</param>
    /// <param name="retryAfter">
    /// An optional suggested delay before the client should retry, surfaced as the <c>Retry-After</c> HTTP response header.
    /// </param>
    /// <param name="innerException">An optional inner exception.</param>
    public KyrolusRateLimitException(string? detail = null, TimeSpan? retryAfter = null, Exception? innerException = null)
        : base(HttpStatusCode.TooManyRequests, KyrolusErrorCodes.RateLimit, "Rate limit exceeded", detail, null, null, true, false, innerException)
    {
        if (retryAfter is { } value)
            WithRetryAfter(value);
    }
}
