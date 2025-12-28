namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusSoftDeleteRepository<TEntity>
    where TEntity : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default);
}
