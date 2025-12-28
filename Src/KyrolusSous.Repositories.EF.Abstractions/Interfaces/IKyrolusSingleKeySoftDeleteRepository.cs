namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

/// <summary>
/// Soft-delete contract لمفتاح واحد.
/// </summary>
public interface IKyrolusSingleKeySoftDeleteRepository<TEntity, TKey>
    where TEntity : class
    where TKey : IEquatable<TKey>
{
    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<RepositoryOperationResult<bool>> TryRestoreAsync(TKey id, CancellationToken cancellationToken = default);

    [RequiresUnreferencedCode("Uses expression tree builders; referenced members must be preserved when trimming.")]
    Task<bool> RestoreAsync(TKey id, CancellationToken cancellationToken = default);
}
