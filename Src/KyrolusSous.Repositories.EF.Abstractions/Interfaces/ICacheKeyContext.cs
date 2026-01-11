namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface ICacheKeyContext
{
    /// <summary>
    /// A stable scope key for cache isolation (e.g. "tenant=...;branch=...").
    /// Return null/empty for global scope.
    /// </summary>
    string? ScopeKey { get; }
}
