namespace KyrolusSous.CQRS.Abstractions.Interfaces;

/// <summary>
/// Defines a specification-based query that returns a list of results.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <typeparam name="TResult">The returned projection or entity type.</typeparam>
public interface IKyrolusSpecificationQuery<TEntity, TResult> : IKyrolusQuery<IReadOnlyList<TResult>>
{
}

/// <summary>
/// Defines a specification-based paginated query that returns a paged result.
/// </summary>
/// <typeparam name="TEntity">The target entity type.</typeparam>
/// <typeparam name="TResult">The returned projection or entity type.</typeparam>
public interface IKyrolusSpecificationPagedQuery<TEntity, TResult> : IKyrolusQuery<Models.KyrolusPagedResult<TResult>>
{
    int PageNumber { get; }
    int PageSize { get; }
}
