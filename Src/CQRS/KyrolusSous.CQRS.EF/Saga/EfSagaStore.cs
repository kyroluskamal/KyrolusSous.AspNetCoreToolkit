using KyrolusSous.CQRS.Saga;

namespace KyrolusSous.CQRS.EF.Saga;

/// <summary>
/// EF Core-backed <see cref="IKyrolusSagaStore"/>: persists <see cref="KyrolusSagaInstance"/> state into
/// <typeparamref name="TDbContext"/> via <see cref="KyrolusSagaInstanceEntity"/>, so a saga survives a
/// process restart - the one thing <see cref="InMemorySagaStore"/>'s own docs say it cannot do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Requires <see cref="KyrolusSagaModelBuilderExtensions.ApplyKyrolusSagaStore"/></b> to have been
/// called from <typeparamref name="TDbContext"/>'s own <c>OnModelCreating</c> - see that method's
/// remarks. Without it, <typeparamref name="TDbContext"/> has no model for
/// <see cref="KyrolusSagaInstanceEntity"/> and every call here throws.
/// </para>
/// <para>
/// <b>Version gating is a manual, count-based conditional update</b> - <c>UPDATE ... WHERE Id = @id AND
/// Version = @expected</c> via <see cref="EntityFrameworkQueryableExtensions.ExecuteUpdateAsync{TSource}"/>,
/// reading the affected-row count back to know whether the write applied - rather than EF's own
/// <c>[ConcurrencyCheck]</c>/row-version machinery. That machinery would require the CONSUMING
/// application's own <typeparamref name="TDbContext"/> model to cooperate (marking a column as a
/// concurrency token), which a store meant to be a one-line drop-in registration cannot assume. A
/// brand-new instance (<see cref="KyrolusSagaInstance.Version"/> of 0) instead goes through a plain
/// <c>Add</c> + <c>SaveChangesAsync</c>, since there is no existing row for an <c>ExecuteUpdateAsync</c>
/// predicate to match in the first place; a primary-key conflict there (another caller's insert for the
/// same id won the race in between) is caught and reported as the same "lost the race" <c>false</c> as
/// a mismatched <see cref="KyrolusSagaInstance.Version"/> would be.
/// </para>
/// <para>
/// <b>Why this takes <see cref="IServiceScopeFactory"/>, not <typeparamref name="TDbContext"/> directly</b>
/// - unlike, say,
/// <see cref="KyrolusSous.CQRS.EF.Behaviors.KyrolusEfTransactionBehavior{TRequest,TResponse,TDbContext}"/>,
/// which does inject <typeparamref name="TDbContext"/> straight into its constructor. That behavior is
/// resolved fresh, per request, by the mediator pipeline, from inside whatever DI scope is already
/// handling the request, so capturing a scoped <typeparamref name="TDbContext"/> there is safe. This
/// store is different:
/// <see cref="KyrolusSous.CQRS.Saga.ServiceCollectionExtensions.AddKyrolusSagaStore{TStore}"/> - the
/// extension point <see cref="Config.KyrolusEfSagaStoreServiceCollectionExtensions.AddKyrolusEfSagaStore{TDbContext}"/>
/// calls to wire this store in - registers <see cref="IKyrolusSagaStore"/> as a singleton (the saga
/// coordinator that depends on it is a singleton too), while a normal <c>AddDbContext&lt;TDbContext&gt;</c>
/// registration is scoped. A singleton that captured a scoped <typeparamref name="TDbContext"/> straight
/// in its own constructor would pin whichever instance happened to be current the first time DI resolved
/// this store, and every save from every saga afterward would silently share that one (not thread-safe)
/// <c>DbContext</c> for the rest of the process's lifetime - the same captive-dependency failure
/// <c>KyrolusMartenTenantSessionFactory</c> (in the sibling Marten package)'s own remarks warn about,
/// for the same underlying reason. Opening a fresh DI scope - and therefore a fresh
/// <typeparamref name="TDbContext"/> - on every call sidesteps that entirely, at the cost of one extra
/// scope per call, which is well within what a saga step (already an I/O-bound operation) can afford.
/// </para>
/// </remarks>
public sealed class EfSagaStore<TDbContext> : IKyrolusSagaStore
    where TDbContext : DbContext
{
    private readonly IServiceScopeFactory _scopeFactory;

    public EfSagaStore(IServiceScopeFactory scopeFactory)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        _scopeFactory = scopeFactory;
    }

    /// <inheritdoc />
    public async Task<bool> SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var set = dbContext.Set<KyrolusSagaInstanceEntity>();

        if (instance.Version == 0)
        {
            set.Add(ToEntity(instance, version: 1));
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException)
            {
                // Another caller already created a row for this id first - the same lost-race
                // outcome the ExecuteUpdateAsync branch below reports via a 0 affected-row count,
                // just surfaced through the primary key constraint instead, since there was no
                // existing row yet for a WHERE clause to match against.
                return false;
            }

            instance.Version = 1;
            return true;
        }

        var newVersion = instance.Version + 1;
        var affected = await set
            .Where(e => e.Id == instance.Id && e.Version == instance.Version)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.SagaName, instance.SagaName)
                .SetProperty(e => e.ContextJson, instance.ContextJson)
                .SetProperty(e => e.CurrentStepIndex, instance.CurrentStepIndex)
                .SetProperty(e => e.Status, instance.Status)
                .SetProperty(e => e.StartedAtUtc, instance.StartedAtUtc)
                .SetProperty(e => e.CompletedAtUtc, instance.CompletedAtUtc)
                .SetProperty(e => e.Error, instance.Error)
                .SetProperty(e => e.CorrelationId, instance.CorrelationId)
                .SetProperty(e => e.StepCountAtStart, instance.StepCountAtStart)
                .SetProperty(e => e.StepSignatureAtStart, instance.StepSignatureAtStart)
                .SetProperty(e => e.Version, newVersion),
                cancellationToken)
            .ConfigureAwait(false);

        if (affected == 0)
            return false;

        instance.Version = newVersion;
        return true;
    }

    /// <inheritdoc />
    public async Task<KyrolusSagaInstance?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var entity = await dbContext.Set<KyrolusSagaInstanceEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == sagaId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KyrolusSagaInstance>> GetIncompleteAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var entities = await dbContext.Set<KyrolusSagaInstanceEntity>()
            .AsNoTracking()
            .Where(e => e.Status == KyrolusSagaStatus.Running || e.Status == KyrolusSagaStatus.Compensating)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(ToDomain).ToList();
    }

    /// <inheritdoc />
    public async Task<KyrolusSagaInstance?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();

        var entity = await dbContext.Set<KyrolusSagaInstanceEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : ToDomain(entity);
    }

    private static KyrolusSagaInstanceEntity ToEntity(KyrolusSagaInstance instance, int version) => new()
    {
        Id = instance.Id,
        SagaName = instance.SagaName,
        ContextJson = instance.ContextJson,
        CurrentStepIndex = instance.CurrentStepIndex,
        Status = instance.Status,
        StartedAtUtc = instance.StartedAtUtc,
        CompletedAtUtc = instance.CompletedAtUtc,
        Error = instance.Error,
        CorrelationId = instance.CorrelationId,
        StepCountAtStart = instance.StepCountAtStart,
        StepSignatureAtStart = instance.StepSignatureAtStart,
        Version = version
    };

    private static KyrolusSagaInstance ToDomain(KyrolusSagaInstanceEntity entity) => new()
    {
        Id = entity.Id,
        SagaName = entity.SagaName,
        ContextJson = entity.ContextJson,
        CurrentStepIndex = entity.CurrentStepIndex,
        Status = entity.Status,
        StartedAtUtc = entity.StartedAtUtc,
        CompletedAtUtc = entity.CompletedAtUtc,
        Error = entity.Error,
        CorrelationId = entity.CorrelationId,
        StepCountAtStart = entity.StepCountAtStart,
        StepSignatureAtStart = entity.StepSignatureAtStart,
        Version = entity.Version
    };
}
