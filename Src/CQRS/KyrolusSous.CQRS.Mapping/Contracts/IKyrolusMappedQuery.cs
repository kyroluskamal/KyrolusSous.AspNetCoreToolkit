namespace KyrolusSous.CQRS.Mapping.Contracts;

/// <summary>
/// Defines a CQRS query that retrieves an entity <typeparamref name="TEntity"/> and projects or maps it to <typeparamref name="TDto"/>.
/// </summary>
/// <typeparam name="TEntity">The underlying domain entity type.</typeparam>
/// <typeparam name="TDto">The mapped destination DTO type.</typeparam>
public interface IKyrolusMappedQuery<TEntity, out TDto> : IKyrolusQuery<TDto>
{
}

/// <summary>
/// Defines a paginated CQRS query that queries <typeparamref name="TEntity"/> and yields a paginated result of <typeparamref name="TDto"/>.
/// </summary>
/// <typeparam name="TEntity">The underlying domain entity type.</typeparam>
/// <typeparam name="TDto">The mapped destination DTO type.</typeparam>
public interface IKyrolusMappedPagedQuery<TEntity, TDto> : IKyrolusQuery<KyrolusPagedResult<TDto>>
{
}

/// <summary>
/// Defines a keyset/seek paginated CQRS query that queries <typeparamref name="TEntity"/> and yields a seek result of <typeparamref name="TDto"/>.
/// </summary>
/// <typeparam name="TEntity">The underlying domain entity type.</typeparam>
/// <typeparam name="TDto">The mapped destination DTO type.</typeparam>
public interface IKyrolusMappedSeekQuery<TEntity, TDto> : IKyrolusQuery<KyrolusSeekResult<TDto>>
{
}

/// <summary>
/// Defines a collection CQRS query that queries <typeparamref name="TEntity"/> and yields a list of <typeparamref name="TDto"/>.
/// </summary>
/// <typeparam name="TEntity">The underlying domain entity type.</typeparam>
/// <typeparam name="TDto">The mapped destination DTO type.</typeparam>
public interface IKyrolusMappedListQuery<TEntity, out TDto> : IKyrolusQuery<IReadOnlyList<TDto>>
{
}
