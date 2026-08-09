namespace KyrolusSous.Caching.Abstractions;

public interface ICacheKeyContext
{
    /// <summary>
    /// A stable scope key for cache isolation (e.g. "tenant=...;branch=...").
    /// Return null/empty for global scope.
    /// </summary>
    string? ScopeKey { get; }

    /// <summary>
    /// Optional cache region (used by cache providers that support regions).
    /// </summary>
    string? Region => null;

    /// <summary>
    /// Optional tenant id (used by cache providers that support tenant isolation).
    /// </summary>
    string? TenantId => null;
}
