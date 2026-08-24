using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Outbox;
using KyrolusSous.Repositories.Marten.Runtime.Repository;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitOfWork;

/// <summary>
/// Unit of work implementation for Marten document sessions with integrated outbox capabilities.
/// </summary>
public sealed class KyrolusMartenUnitOfWork<TSession>(
    TSession session,
    IServiceProvider? serviceProvider = null,
    Func<Type, object?>? repositoryFactory = null) : IKyrolusMartenUnitOfWork<TSession>, IKyrolusMartenOutboxStore
    where TSession : class, IDocumentSession
{
    private readonly TSession session = session ?? throw new ArgumentNullException(nameof(session));
    private readonly IServiceProvider? serviceProvider = serviceProvider;
    private readonly Func<Type, object?>? repositoryFactory = repositoryFactory;
    private readonly Dictionary<Type, object> cache = [];
    private bool disposed;

    public TRepo GetRepository<TRepo>() where TRepo : class
    {
        ObjectDisposedException.ThrowIf(disposed, this);

        var type = typeof(TRepo);
        if (cache.TryGetValue(type, out var existing)) return (TRepo)existing;

        object? repo = repositoryFactory?.Invoke(type);
        repo ??= BuildRepository(type);
        repo ??= serviceProvider?.GetService(type);
        if (repo is null) throw new InvalidOperationException($"Repository of type '{type.FullName}' is not registered.");

        cache[type] = repo;
        return (TRepo)repo;
    }

    private object? BuildRepository(Type type)
    {
        if (serviceProvider is null) return null;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IKyrolusMartenRepositoryAsync<,,>))
        {
            var args = type.GetGenericArguments();
            var factory = typeof(KyrolusMartenRepositoryFactory)
                .GetMethod(nameof(KyrolusMartenRepositoryFactory.Create), BindingFlags.Public | BindingFlags.Static)!
                .MakeGenericMethod(args);
            return factory.Invoke(null, [serviceProvider, session, null, true]);
        }
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IKyrolusMartenSoftDeleteRepositoryAsync<,,>))
        {
            var args = type.GetGenericArguments();
            var innerType = typeof(KyrolusMartenSoftDeleteRepositoryAsync<,,>).MakeGenericType(args);
            var deps = new KyrolusMartenRepositoryDependencies(
                Observer: serviceProvider.GetService<IKyrolusMartenObserver>(),
                Authorization: serviceProvider.GetService<IKyrolusMartenAuthorization>(),
                Validation: serviceProvider.GetService<IKyrolusMartenValidation>(),
                SoftDeletePolicy: serviceProvider.GetService<IKyrolusMartenSoftDeletePolicy>(),
                CacheProvider: serviceProvider.GetService<ICacheProvider>(),
                CacheKeyContext: serviceProvider.GetService<ICacheKeyContext>(),
                CachePolicyProvider: serviceProvider.GetService<IKyrolusRepositoryCachePolicyProvider>(),
                PolicyProvider: serviceProvider.GetService<IKyrolusMartenRepositoryPolicyProvider>(),
                ResiliencePolicy: serviceProvider.GetService<IKyrolusMartenResiliencePolicy>(),
                Tracing: serviceProvider.GetService<IKyrolusMartenTracing>());
            return ActivatorUtilities.CreateInstance(serviceProvider, innerType, session, deps);
        }
        return null;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return 1;
    }

    public Task EnqueueAsync(KyrolusMartenOutboxMessage message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(message);
        session.Store(message);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<KyrolusMartenOutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var size = batchSize <= 0 ? 100 : batchSize;
        var list = await session.Query<KyrolusMartenOutboxMessage>()
            .Where(x => !x.Processed)
            .OrderBy(x => x.OccurredOnUtc)
            .Take(size)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return list;
    }

    public async Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var msg = await session.LoadAsync<KyrolusMartenOutboxMessage>(messageId, cancellationToken).ConfigureAwait(false);
        if (msg is not null)
        {
            msg.Processed = true;
            msg.ProcessedAtUtc = DateTime.UtcNow;
            session.Store(msg);
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
