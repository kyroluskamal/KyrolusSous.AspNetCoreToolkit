using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Runtime.Repository;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;

public sealed class KyrolusMartenUnitOfWork<TSession>(
    TSession session,
    IServiceProvider? serviceProvider = null,
    Func<Type, object?>? repositoryFactory = null) : IKyrolusMartenUnitOfWork<TSession>
    where TSession : class, IDocumentSession
{
    private readonly TSession session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly IServiceProvider? serviceProvider = serviceProvider;
    private readonly Func<Type, object?>? repositoryFactory = repositoryFactory;
    private readonly Dictionary<Type, object> cache = [];
    private bool disposed;

    public TRepo GetRepository<TRepo>() where TRepo : class
    {
        var type = typeof(TRepo);
        if (cache.TryGetValue(type, out var existing)) return (TRepo)existing;

        object? repo = repositoryFactory?.Invoke(type);
        repo ??= serviceProvider?.GetService(type);
        repo ??= BuildRepository(type);
        if (repo is null) throw new InvalidOperationException($"Repository of type '{type.FullName}' is not registered.");

        cache[type] = repo;
        return (TRepo)repo;
    }

    private object? BuildRepository(Type type)
    {
        if (serviceProvider is null) return null;
        // handle IKyrolusMartenRepositoryAsync<TSession,TEntity,TKey>
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IKyrolusMartenRepositoryAsync<,,>))
        {
            var args = type.GetGenericArguments();
            var factory = typeof(KyrolusMartenRepositoryFactory)
                .GetMethod(nameof(KyrolusMartenRepositoryFactory.Create), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(args);
            return factory.Invoke(null, [serviceProvider, session, null, true]);
        }
        // handle soft delete specialization
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IKyrolusMartenSoftDeleteRepositoryAsync<,,>))
        {
            var args = type.GetGenericArguments();
            var innerType = typeof(KyrolusMartenSoftDeleteRepositoryAsync<,,>).MakeGenericType(args);
            return ActivatorUtilities.CreateInstance(serviceProvider, innerType, session, new KyrolusMartenRepositoryDependencies());
        }
        return null;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            sw.Stop();
            return 1;
        }
        finally
        {
            // no observer here; keep minimal for runtime baseline
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        cache.Clear();
        session.Dispose();
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        cache.Clear();
        await session.DisposeAsync().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }
}
