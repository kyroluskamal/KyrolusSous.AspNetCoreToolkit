namespace KyrolusSous.Repositories.EF.Abstractions.Concurrency;

/// <summary>
/// Specifies the conflict resolution strategy when a <see cref="DbUpdateConcurrencyException"/> occurs.
/// </summary>
public enum KyrolusConcurrencyStrategy
{
    /// <summary>
    /// Bubble up the concurrency exception (fail-fast default).
    /// </summary>
    ThrowException,

    /// <summary>
    /// Overwrite database values with current client values.
    /// </summary>
    ClientWins,

    /// <summary>
    /// Discard client values and retain values currently stored in the database.
    /// </summary>
    DatabaseWins,

    /// <summary>
    /// Merge modified non-conflicting properties from client while accepting database values for unchanged properties.
    /// </summary>
    Merge
}

/// <summary>
/// Defines a resolver for optimistic concurrency conflicts.
/// </summary>
public interface IKyrolusConcurrencyResolver
{
    /// <summary>
    /// Resolves concurrency conflicts on the specified database context using the given strategy.
    /// </summary>
    Task<bool> ResolveConcurrencyConflictAsync(
        DbContext context,
        DbUpdateConcurrencyException exception,
        KyrolusConcurrencyStrategy strategy,
        CancellationToken cancellationToken = default);
}
