using System.Diagnostics.CodeAnalysis;

namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusBulkRepository<TEntity>
    where TEntity : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<int> BulkInsertAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    Task<int> BulkUpsertAsync(IEnumerable<TEntity> entities, Expression<Func<TEntity, bool>> matchOn, CancellationToken cancellationToken = default);
}
