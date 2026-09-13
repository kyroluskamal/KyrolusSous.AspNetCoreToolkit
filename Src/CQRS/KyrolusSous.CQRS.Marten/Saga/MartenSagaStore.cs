using KyrolusSous.CQRS.Saga;

namespace KyrolusSous.CQRS.Marten.Saga;

/// <summary>
/// Marten-backed <see cref="IKyrolusSagaStore"/>: persists <see cref="KyrolusSagaInstance"/> state as a
/// <see cref="KyrolusMartenSagaInstanceDocument"/>, so a saga survives a process restart - the one thing
/// <see cref="InMemorySagaStore"/>'s own docs say it cannot do.
/// </summary>
/// <remarks>
/// <para>
/// <b>Version gating rides Marten's own optimistic concurrency, not a manual comparison.</b> Unlike
/// <c>EfSagaStore&lt;TDbContext&gt;</c> (in the sibling EF package), which has to compare
/// <see cref="KyrolusSagaInstance.Version"/> against a stored column itself - EF has no portable way to
/// make that check happen server-side without the consuming application's own <c>DbContext</c> model
/// cooperating, see that type's remarks - <see cref="KyrolusMartenSagaInstanceDocument"/> implements
/// <see cref="JasperFx.IRevisioned"/>, so Marten enforces the exact same "write only if the stored
/// revision still matches what was read" rule itself, server-side, on every plain
/// <c>session.Store(document)</c> - no explicit <c>WHERE</c> clause or affected-row count needed here.
/// A revision mismatch surfaces as <see cref="JasperFx.ConcurrencyException"/> from
/// <c>SaveChangesAsync()</c>, which <see cref="MartenSagaStore.SaveAsync"/> catches and translates to
/// <see langword="false"/>, matching <see cref="IKyrolusSagaStore.SaveAsync"/>'s contract. A brand-new
/// id (<see cref="KyrolusSagaInstance.Version"/> of 0) is not special-cased separately either: Marten's
/// own revisioned-document upsert treats <c>Version == 0</c> as "insert, starting at revision 1" when no
/// row exists yet for that id, and as an ordinary - and, for a genuinely new id whose row was created by
/// a concurrent caller in the meantime, correctly rejected - revision check otherwise.
/// </para>
/// <para>
/// <b>Why this opens a fresh session per call instead of taking one injected.</b>
/// <see cref="KyrolusSous.CQRS.Saga.ServiceCollectionExtensions.AddKyrolusSagaStore{TStore}"/> - the
/// extension point <see cref="Config.KyrolusMartenSagaStoreServiceCollectionExtensions.UseKyrolusMartenSagaStore"/>
/// calls to wire this store in - registers <see cref="IKyrolusSagaStore"/> as a singleton, but Marten's
/// own <c>AddMarten(...)</c> registers <see cref="IDocumentSession"/> scoped by default (see
/// <see cref="MultiTenancy.KyrolusMartenTenantSessionFactory"/>'s remarks on exactly this kind of
/// lifetime mismatch). Depending on <see cref="IDocumentStore"/> instead - itself always a singleton,
/// and the type Marten's own long-lived background services are built to hold onto - and opening a
/// <c>LightweightSession()</c>/<c>QuerySession()</c> per call sidesteps that captive-dependency trap
/// entirely.
/// </para>
/// </remarks>
public sealed class MartenSagaStore : IKyrolusSagaStore
{
    private readonly IDocumentStore _store;

    public MartenSagaStore(IDocumentStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Does not need to distinguish "insert" from "update" the way
    /// <c>EfSagaStore&lt;TDbContext&gt;.SaveAsync</c> (in the sibling EF package) does - see this
    /// type's remarks for why a single <c>Store</c> + <c>SaveChangesAsync</c> call handles both.
    /// </remarks>
    public async Task<bool> SaveAsync(KyrolusSagaInstance instance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instance);

        await using var session = _store.LightweightSession();
        var document = ToDocument(instance);
        session.Store(document);

        try
        {
            await session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (JasperFx.ConcurrencyException)
        {
            return false;
        }

        instance.Version = document.Version; // Marten updated this in place to the new revision
        return true;
    }

    /// <inheritdoc />
    public async Task<KyrolusSagaInstance?> GetAsync(Guid sagaId, CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession();
        var document = await session.LoadAsync<KyrolusMartenSagaInstanceDocument>(sagaId, cancellationToken).ConfigureAwait(false);
        return document is null ? null : ToDomain(document);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KyrolusSagaInstance>> GetIncompleteAsync(CancellationToken cancellationToken = default)
    {
        await using var session = _store.QuerySession();
        var documents = await session.Query<KyrolusMartenSagaInstanceDocument>()
            .Where(d => d.Status == KyrolusSagaStatus.Running || d.Status == KyrolusSagaStatus.Compensating)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return documents.Select(ToDomain).ToList();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Queries against <see cref="KyrolusMartenSagaInstanceDocument.CorrelationId"/>'s duplicated,
    /// indexed column rather than the JSONB <c>data</c> column - see that property's remarks.
    /// </remarks>
    public async Task<KyrolusSagaInstance?> GetByCorrelationIdAsync(string correlationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        await using var session = _store.QuerySession();
        var document = await session.Query<KyrolusMartenSagaInstanceDocument>()
            .FirstOrDefaultAsync(d => d.CorrelationId == correlationId, cancellationToken)
            .ConfigureAwait(false);

        return document is null ? null : ToDomain(document);
    }

    private static KyrolusMartenSagaInstanceDocument ToDocument(KyrolusSagaInstance instance) => new()
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
        Version = instance.Version
    };

    private static KyrolusSagaInstance ToDomain(KyrolusMartenSagaInstanceDocument document) => new()
    {
        Id = document.Id,
        SagaName = document.SagaName,
        ContextJson = document.ContextJson,
        CurrentStepIndex = document.CurrentStepIndex,
        Status = document.Status,
        StartedAtUtc = document.StartedAtUtc,
        CompletedAtUtc = document.CompletedAtUtc,
        Error = document.Error,
        CorrelationId = document.CorrelationId,
        StepCountAtStart = document.StepCountAtStart,
        StepSignatureAtStart = document.StepSignatureAtStart,
        Version = document.Version
    };
}
