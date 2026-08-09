namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusRepositoryAsync<TDbcontext, TEntity, TKey>
    where TEntity : class
    where TDbcontext : DbContext
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        List<string>? includeProperties = null,
        IncludeGraph<TEntity>? includeGraph = null,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? filter,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy,
        bool? asNoTracking = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity> AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IEnumerable<TEntity>> AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<TEntity> UpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<IEnumerable<TEntity>> UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RemoveAsync(TEntity entity, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> ExistAsync(Expression<Func<TEntity, bool>> filter, CancellationToken cancellationToken = default);

    // Advanced querying
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    IAsyncEnumerable<TEntity> StreamAsync(Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool asNoTracking = true,
        bool useSplitQuery = false,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<List<TResult>> QueryAsync<TResult>(IKyrolusQuerySpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<List<TResult>> QueryAsync<TResult>(Expression<Func<TEntity, bool>>? filter,
    Expression<Func<TEntity, TResult>> selector,
    Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null, bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<(IReadOnlyList<TResult> Items, int TotalCount)> GetPagedAsync<TResult>(IKyrolusPagedQuerySpecification<TEntity, TResult> specification, CancellationToken cancellationToken = default);

    // Optional paging using policy defaults when pageSize/pageNumber are null
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<(IReadOnlyList<TEntity> Items, int TotalCount)> GetPagedWithDefaultsAsync<TResult>(IKyrolusPagedQuerySpecification<TEntity, TResult> specification,
        Expression<Func<TEntity, bool>>? filter = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        bool? asNoTracking = null, bool? useSplitQuery = null,
        CancellationToken cancellationToken = default,
        params Expression<Func<TEntity, object?>>[] includeExpressions);

    // Server-side bulk-like operations (always available; bulkExecutor optional)
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>>? filter,
        Action<UpdateSettersBuilder<TEntity>> setPropertyCalls,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>>? filter = null, bool? useSplitQuery = null, CancellationToken cancellationToken = default);

    // Try operations (no SaveChanges here; UoW will save)
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<TEntity>> TryUpdateAsync(TEntity entity, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TryRemoveAsync(TEntity entity, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<List<TEntity>> GetAllCompiledAsync(Expression<Func<TEntity, bool>> filter,
        bool? asNoTracking = null,
        bool? useSplitQuery = null,
        CancellationToken cancellationToken = default);
}
