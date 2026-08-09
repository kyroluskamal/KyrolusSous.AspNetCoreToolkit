namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenUnitOfWork<TSession> : IDisposable, IAsyncDisposable
    where TSession : class
{
    /// <summary>
    /// Resolve a repository scoped to the same document session.
    /// </summary>
    /// <typeparam name="TRepo">Concrete repository type.</typeparam>
    /// <returns>Repository instance.</returns>
    TRepo GetRepository<TRepo>() where TRepo : class;

    /// <summary>
    /// Persist pending changes.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
