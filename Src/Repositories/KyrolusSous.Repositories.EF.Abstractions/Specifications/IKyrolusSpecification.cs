namespace KyrolusSous.Repositories.EF.Abstractions.Specifications;

/// <summary>
/// Defines a composable specification for querying <typeparamref name="TEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The entity type.</typeparam>
public interface IKyrolusSpecification<TEntity>
    where TEntity : class
{
    /// <summary>
    /// Gets the filtering criteria expression.
    /// </summary>
    Expression<Func<TEntity, bool>>? Criteria { get; }

    /// <summary>
    /// Gets strongly-typed navigation property includes.
    /// </summary>
    IReadOnlyList<Expression<Func<TEntity, object>>> Includes { get; }

    /// <summary>
    /// Gets string-based navigation property includes.
    /// </summary>
    IReadOnlyList<string> IncludeStrings { get; }

    /// <summary>
    /// Gets the primary ascending sort expression.
    /// </summary>
    Expression<Func<TEntity, object>>? OrderBy { get; }

    /// <summary>
    /// Gets the primary descending sort expression.
    /// </summary>
    Expression<Func<TEntity, object>>? OrderByDescending { get; }

    /// <summary>
    /// Gets the number of items to take (for paging).
    /// </summary>
    int? Take { get; }

    /// <summary>
    /// Gets the number of items to skip (for paging).
    /// </summary>
    int? Skip { get; }

    /// <summary>
    /// Gets whether paging is enabled.
    /// </summary>
    bool IsPagingEnabled { get; }

    /// <summary>
    /// Gets whether split queries (<c>AsSplitQuery</c>) should be used.
    /// </summary>
    bool IsSplitQuery { get; }

    /// <summary>
    /// Gets whether change tracking is disabled (<c>AsNoTracking</c>).
    /// </summary>
    bool IsNoTracking { get; }
}
