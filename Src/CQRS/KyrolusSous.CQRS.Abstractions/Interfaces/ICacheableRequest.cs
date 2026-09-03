namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Marks a query (or the command that should invalidate it) as eligible for the query-caching /
/// cache-invalidation behaviors.
/// </summary>
public interface IKyrolusCacheableRequest
{
    public bool Cacheable { get; set; }

    /// <summary>
    /// Whether the cached result may be shared across every caller, rather than scoped to the current
    /// user and tenant. Defaults to <see langword="false"/>: caching is scoped per user/tenant unless
    /// a request opts out explicitly, because a query with no per-user data of its own (an admin
    /// report, a product catalog) is the exception, and "cached once, served to whoever asks next" is
    /// the wrong default for anything else - it silently returns one user's data to another.
    /// </summary>
    bool IsSharedAcrossUsers => false;
}
