namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusUnitOfWork : IDisposable, IAsyncDisposable
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<int>> SaveChangesWithRetryAsync(string? rowVersionPropertyName = null, CancellationToken cancellationToken = default);
    Task<RepositoryOperationResult<int>> ExecuteAsync(Func<Task> work, bool useTransaction = true, string? rowVersionPropertyName = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve a repository instance (runtime or generated) scoped to the same DbContext/UoW.
    /// </summary>
    /// <typeparam name="TRepo">Repository concrete type to resolve.</typeparam>
    /// <returns>An instance of the requested repository.</returns>
    TRepo GetRepository<TRepo>() where TRepo : class;

    /// <summary>
    /// Resolve a repository by name and cast to the requested type (useful when the name is dynamic but you still want IntelliSense).
    /// </summary>
    /// <typeparam name="TRepo">Repository concrete type to resolve.</typeparam>
    /// <param name="name">Repository name (usually entity type name).</param>
    /// <returns>Repository instance if found; otherwise null.</returns>
    TRepo? GetRepository<TRepo>(string name) where TRepo : class;
}
