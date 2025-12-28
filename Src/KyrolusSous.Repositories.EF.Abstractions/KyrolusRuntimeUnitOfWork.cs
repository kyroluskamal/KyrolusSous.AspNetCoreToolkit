namespace KyrolusSous.Repositories.EF.Abstractions;

/// <summary>
/// Generic Unit of Work implementation usable without the source generator.
/// It resolves repositories from DI or an optional factory and caches them per scope.
/// </summary>
public sealed class KyrolusRuntimeUnitOfWork<TDbContext> : IKyrolusUnitOfWork
    where TDbContext : DbContext
{
    private readonly TDbContext db;
    private readonly KyrolusRepositoryPolicy policy;
    private readonly IKyrolusRepositoryObserver? observer;
    private readonly IServiceProvider? serviceProvider;
    private readonly Func<Type, object?>? repositoryFactory;
    private readonly Dictionary<Type, object> repositoryCache = [];
    private bool disposed;

    public KyrolusRuntimeUnitOfWork(
        TDbContext db,
        KyrolusRepositoryPolicy? policy = null,
        IKyrolusRepositoryObserver? observer = null,
        IServiceProvider? serviceProvider = null,

        Func<Type, object?>? repositoryFactory = null)
    {
        this.db = db ?? throw new ArgumentNullException(nameof(db));
        this.policy = policy ?? KyrolusRepositoryPolicy.Default;
        this.observer = observer;
        this.serviceProvider = serviceProvider;
        this.repositoryFactory = repositoryFactory;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (observer is not null) await observer.OnBeforeAsync("SaveChanges", null, cancellationToken).ConfigureAwait(false);
        try
        {
            var affected = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            if (observer is not null) await observer.OnAfterAsync("SaveChanges", affected, sw.Elapsed, null, cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (observer is not null) await observer.OnAfterAsync("SaveChanges", null, sw.Elapsed, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public Task<RepositoryOperationResult<int>> SaveChangesWithRetryAsync(string? rowVersionPropertyName = null, CancellationToken cancellationToken = default)
    {
        return ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            async () =>
            {
                var sw = Stopwatch.StartNew();
                if (observer is not null) await observer.OnBeforeAsync("SaveChangesWithRetry", null, cancellationToken).ConfigureAwait(false);
                var affected = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                sw.Stop();
                if (observer is not null) await observer.OnAfterAsync("SaveChangesWithRetry", affected, sw.Elapsed, null, cancellationToken).ConfigureAwait(false);
                return affected;
            },
            policy,
            ex => ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionPropertyName, cancellationToken),
            cancellationToken);
    }

    public async Task<RepositoryOperationResult<int>> ExecuteAsync(Func<Task> work, bool useTransaction = true, string? rowVersionPropertyName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!useTransaction)
        {
            await work().ConfigureAwait(false);
            return await SaveChangesWithRetryAsync(rowVersionPropertyName, cancellationToken).ConfigureAwait(false);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await work().ConfigureAwait(false);
        var result = await SaveChangesWithRetryAsync(rowVersionPropertyName, cancellationToken).ConfigureAwait(false);
        if (result.Status == KyrolusRepositoryOperationStatus.Success)
        {
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        return result;
    }

    public TRepo GetRepository<TRepo>() where TRepo : class
    {
        var type = typeof(TRepo);
        if (repositoryCache.TryGetValue(type, out var cached))
        {
            return (TRepo)cached;
        }

        object? repo = repositoryFactory?.Invoke(type);
        if (repo is null && serviceProvider is not null)
        {
            repo = serviceProvider.GetService(type);
        }

        if (repo is null)
        {
            throw new InvalidOperationException($"Repository of type '{type.FullName}' is not registered. Provide a factory or register it in DI.");
        }

        repositoryCache[type] = repo;
        return (TRepo)repo;
    }

    public TRepo? GetRepository<TRepo>(string name) where TRepo : class
    {
        // name is ignored in the generic runtime UoW; resolution is by TRepo.
        return GetRepository<TRepo>();
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        repositoryCache.Clear();
        db.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        repositoryCache.Clear();
        await db.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
