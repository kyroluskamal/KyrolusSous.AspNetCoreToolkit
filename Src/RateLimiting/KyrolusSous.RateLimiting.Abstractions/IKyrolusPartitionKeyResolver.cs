using Microsoft.AspNetCore.Http;

namespace KyrolusSous.RateLimiting.Abstractions;

/// <summary>
/// Defines a contract for resolving rate limiting partition keys from an HTTP context.
/// </summary>
public interface IKyrolusPartitionKeyResolver
{
    /// <summary>
    /// Resolves a partition key string from the active HTTP context.
    /// </summary>
    /// <param name="context">The active HTTP context.</param>
    /// <returns>A normalized partition key identifier.</returns>
    string ResolveKey(HttpContext context);
}
