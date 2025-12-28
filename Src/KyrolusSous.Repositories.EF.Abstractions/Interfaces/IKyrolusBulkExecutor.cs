using Microsoft.EntityFrameworkCore.Query;

namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusBulkExecutor<TEntity>
    where TEntity : class
{
    Task<int> ExecuteUpdateAsync(Expression<Func<TEntity, bool>>? filter,
        Action<UpdateSettersBuilder<TEntity>> setPropertyCalls,
        bool useSplitQuery,
        CancellationToken cancellationToken);
    Task<int> ExecuteDeleteAsync(Expression<Func<TEntity, bool>>? filter, bool useSplitQuery, CancellationToken cancellationToken);
    Task<int> BulkInsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken);
    Task<int> BulkUpsertAsync(IEnumerable<TEntity> entities, Expression<Func<TEntity, bool>> matchOn, CancellationToken cancellationToken);
}
