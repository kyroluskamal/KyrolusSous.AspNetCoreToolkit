namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

/// <summary>
/// Soft-delete contract لمفاتيح مركبة.
/// </summary>
public interface IKyrolusCompositeKeySoftDeleteRepository<TEntity>
    where TEntity : class
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TryRestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RestoreAsync(object?[]? keyValues, CancellationToken cancellationToken = default);
}
