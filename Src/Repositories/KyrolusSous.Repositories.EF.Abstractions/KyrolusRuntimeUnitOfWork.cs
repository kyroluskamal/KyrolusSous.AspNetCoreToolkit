namespace KyrolusSous.Repositories.EF.Abstractions;

/// <summary>
/// Generic Unit of Work implementation usable without the source generator.
/// It resolves repositories from DI or an optional factory and caches them per scope.
/// </summary>
public sealed class KyrolusRuntimeUnitOfWork<TDbContext>(
    TDbContext db,
    KyrolusRepositoryPolicy? policy = null,
    IKyrolusRepositoryObserver? observer = null,
    IServiceProvider? serviceProvider = null,
    Func<Type, object?>? repositoryFactory = null) : IKyrolusUnitOfWork
    where TDbContext : DbContext
{
    private readonly TDbContext db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly KyrolusRepositoryPolicy policy = policy ?? KyrolusRepositoryPolicy.Default;
    private readonly IKyrolusRepositoryObserver? observer = observer;
    private readonly IServiceProvider? serviceProvider = serviceProvider;
    private readonly Func<Type, object?>? repositoryFactory = repositoryFactory;
    private readonly Dictionary<Type, object> repositoryCache = [];
    private bool disposed;

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        if (observer is not null) await observer.OnBeforeAsync(nameof(SaveChangesAsync), null, cancellationToken).ConfigureAwait(false);
        try
        {
            var affected = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            if (observer is not null) await observer.OnAfterAsync(nameof(SaveChangesAsync), affected, sw.Elapsed, null, cancellationToken).ConfigureAwait(false);
            return affected;
        }
        catch (Exception ex)
        {
            sw.Stop();
            if (observer is not null) await observer.OnAfterAsync(nameof(SaveChangesAsync), null, sw.Elapsed, ex, cancellationToken).ConfigureAwait(false);
            throw;
        }
    }

    public Task<RepositoryOperationResult<int>> SaveChangesWithRetryAsync(string? rowVersionPropertyName = null, CancellationToken cancellationToken = default)
    {
        return ConcurrencyHelper.ExecuteWithConcurrencyRetryAsync(
            async () =>
            {
                var sw = Stopwatch.StartNew();
                if (observer is not null) await observer.OnBeforeAsync(nameof(SaveChangesWithRetryAsync), null, cancellationToken).ConfigureAwait(false);
                var affected = await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                sw.Stop();
                if (observer is not null) await observer.OnAfterAsync(nameof(SaveChangesWithRetryAsync), affected, sw.Elapsed, null, cancellationToken).ConfigureAwait(false);
                return affected;
            },
            policy,
            ex => ConcurrencyHelper.BuildConcurrencyInfoAsync(ex, rowVersionPropertyName, cancellationToken),
            cancellationToken);
    }

    public async Task<RepositoryOperationResult<int>> ExecuteAsync(Func<Task> work, bool useTransaction = true, string? rowVersionPropertyName = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        if (!useTransaction || db.Database.CurrentTransaction is not null)
        {
            await work().ConfigureAwait(false);
            return await SaveChangesWithRetryAsync(rowVersionPropertyName, cancellationToken).ConfigureAwait(false);
        }

        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await work().ConfigureAwait(false);
        var result = await SaveChangesWithRetryAsync(rowVersionPropertyName, cancellationToken).ConfigureAwait(false);
        if (result.Status == KyrolusRepositoryOperationStatus.Success)
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }

    public TRepo GetRepository<TRepo>() where TRepo : class
    {
        var type = typeof(TRepo);
        if (repositoryCache.TryGetValue(type, out var cached))
            return (TRepo)cached;

        object? repo = repositoryFactory?.Invoke(type);
        if (repo is null && serviceProvider is not null)
            repo = serviceProvider.GetService(type);

        if (repo is null)
            throw new InvalidOperationException($"Repository of type '{type.FullName}' is not registered. Provide a factory or register it in DI.");

        repositoryCache[type] = repo;
        return (TRepo)repo;
    }

    public TRepo? GetRepository<TRepo>(string name) where TRepo : class
    {
        if (string.IsNullOrWhiteSpace(name))            return GetRepository<TRepo>();

        name = name.Trim();
        var requestedType = typeof(TRepo);
        if (NameMatches(requestedType, name))            return GetRepository<TRepo>();

        var repoType = ResolveRepositoryTypeByName(name);
        if (repoType is null || !requestedType.IsAssignableFrom(repoType))
            return null;

        if (repositoryCache.TryGetValue(repoType, out var cached))
            return (TRepo)cached;

        object? repo = repositoryFactory?.Invoke(repoType);
        if (repo is null && serviceProvider is not null)
            repo = serviceProvider.GetService(repoType);

        if (repo is null)            return null;

        repositoryCache[repoType] = repo;
        return (TRepo)repo;
    }

    private static Type? ResolveRepositoryTypeByName(string name)
    {
        var map = KyrolusRuntimeRepositoryTypeMap.Map.Value;
        return map.TryGetValue(name, out var type) ? type : null;
    }

    private static bool NameMatches(Type type, string name)
    => string.Equals(name, type.Name, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, type.FullName, StringComparison.OrdinalIgnoreCase);

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

internal static class KyrolusRuntimeRepositoryTypeMap
{
    public static readonly Lazy<Dictionary<string, Type>> Map = new(BuildRepositoryTypeMap);

    private static Dictionary<string, Type> BuildRepositoryTypeMap()
    {
        var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        var assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (var i = 0; i < assemblies.Length; i++)
        {
            Type[] types;
            try
            {
                types = assemblies[i].GetTypes();
            }
            catch (System.Reflection.ReflectionTypeLoadException ex)
            {
                types = ex.Types.Where(static t => t is not null).ToArray()!;
            }
            catch
            {
                continue;
            }

            for (var t = 0; t < types.Length; t++)
            {
                var type = types[t];
                if (type is null || !type.IsClass || type.IsAbstract || type.ContainsGenericParameters || !ImplementsRepositoryInterface(type))
                    continue;

                map.TryAdd(type.Name, type);
                if (!string.IsNullOrWhiteSpace(type.FullName))
                    map.TryAdd(type.FullName!, type);
            }
        }

        return map;
    }

    private static bool ImplementsRepositoryInterface(Type type)
    {
        var interfaces = type.GetInterfaces();
        for (var i = 0; i < interfaces.Length; i++)
        {
            var iface = interfaces[i];
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IKyrolusRepositoryAsync<,,>))
                return true;
        }

        return false;
    }
}
