using KyrolusSous.CQRS.Marten.Saga;
using KyrolusSous.CQRS.Saga;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Covers <see cref="MartenSagaStore"/> against substituted <see cref="IDocumentStore"/>/
/// <see cref="IDocumentSession"/>/<see cref="IQuerySession"/> objects, the same style
/// <see cref="KyrolusMartenTenantSessionFactoryTests"/> uses for the same reason: this repository has no
/// real Postgres instance available in this environment (no reachable server, no Testcontainers
/// dependency in the repo, and the local Docker daemon is not running), so nothing Marten-backed can be
/// exercised end-to-end here the way <c>EfSagaStoreTests</c> exercises <c>EfSagaStore</c> against a real
/// (SQLite) relational provider.
/// </summary>
/// <remarks>
/// What this leaves uncovered, and why: <see cref="MartenSagaStore.GetIncompleteAsync"/> and
/// <see cref="MartenSagaStore.GetByCorrelationIdAsync"/> both call <c>session.Query&lt;T&gt;()</c>,
/// which returns Marten's own LINQ provider (<c>IMartenQueryable&lt;T&gt;</c>) - a substituted
/// <see cref="IQuerySession"/> cannot produce a working one of these (unlike <c>LoadAsync</c>, which is
/// a plain method NSubstitute can stub directly), so those two methods - and a genuine end-to-end
/// crash-resume proof through <see cref="KyrolusSagaCoordinator"/> - are not covered by an executable
/// test in this environment. What IS verified for them: they use the exact same
/// <c>session.Query&lt;T&gt;().Where(...).ToListAsync(cancellationToken)</c> /
/// <c>.FirstOrDefaultAsync(...)</c> idiom already shipping (and exercised by this repository's own
/// production code path) in <c>KyrolusMartenRepositoryAsync</c> and <c>KyrolusMartenUnitOfWork</c>; and,
/// via reflection against the actually-installed Marten 9.20.2 / JasperFx 2.36.2 assemblies during
/// development of this store, that every Marten member this store calls
/// (<c>LightweightSession()</c>, <c>QuerySession()</c>, <c>LoadAsync&lt;T&gt;</c>, <c>Query&lt;T&gt;()</c>,
/// <c>SaveChangesAsync(CancellationToken)</c>) and <see cref="JasperFx.IRevisioned.Version"/>'s type
/// (<see cref="int"/>, matching <see cref="KyrolusSagaInstance.Version"/> exactly) have precisely the
/// signatures this store's code assumes, and that the exception Marten throws on a revision conflict is
/// <see cref="JasperFx.ConcurrencyException"/> (not, say, <c>Marten.Exceptions.ConcurrencyException</c>,
/// which no longer exists in this Marten version).
/// </remarks>
public sealed class MartenSagaStoreTests
{
    [Fact(DisplayName = "MartenSagaStore: constructor rejects a null IDocumentStore")]
    public void Constructor_NullStore_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new MartenSagaStore(null!));
    }

    [Fact(DisplayName = "MartenSagaStore: SaveAsync rejects a null instance")]
    public async Task SaveAsync_NullInstance_Throws()
    {
        var store = new MartenSagaStore(Substitute.For<IDocumentStore>());
        await Should.ThrowAsync<ArgumentNullException>(() => store.SaveAsync(null!));
    }

    [Fact(DisplayName = "MartenSagaStore: SaveAsync stores the mapped document, saves the session, and adopts Marten's post-save revision as the instance's new Version")]
    public async Task SaveAsync_Success_StoresDocumentAndAdoptsRevisionedVersion()
    {
        var documentStore = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        documentStore.LightweightSession().Returns(session);

        KyrolusMartenSagaInstanceDocument? captured = null;
        session.When(s => s.Store(Arg.Any<KyrolusMartenSagaInstanceDocument[]>()))
            .Do(call => captured = ((KyrolusMartenSagaInstanceDocument[])call.Args()[0]!).Single());

        // Simulates the one thing this store leans on Marten itself to do: after a successful
        // SaveChangesAsync, a revisioned document's own Version property has been advanced to the new
        // stored revision - see MartenSagaStore.SaveAsync's remarks.
        session.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                captured!.Version += 1;
                return Task.CompletedTask;
            });

        var store = new MartenSagaStore(documentStore);
        var instance = new KyrolusSagaInstance { SagaName = "Order", ContextJson = "{\"a\":1}", CorrelationId = "corr-1" };

        (await store.SaveAsync(instance)).ShouldBeTrue();

        captured.ShouldNotBeNull();
        captured.Id.ShouldBe(instance.Id);
        captured.SagaName.ShouldBe("Order");
        captured.ContextJson.ShouldBe("{\"a\":1}");
        captured.CorrelationId.ShouldBe("corr-1");
        instance.Version.ShouldBe(1); // adopted from the document Marten "revisioned" in place
    }

    [Fact(DisplayName = "MartenSagaStore: SaveAsync translates JasperFx.ConcurrencyException into false and leaves the caller's instance untouched")]
    public async Task SaveAsync_ConcurrencyException_ReturnsFalse_LeavesInstanceUntouched()
    {
        var documentStore = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IDocumentSession>();
        documentStore.LightweightSession().Returns(session);

        session.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new JasperFx.ConcurrencyException(typeof(KyrolusMartenSagaInstanceDocument), Guid.NewGuid()));

        var store = new MartenSagaStore(documentStore);
        var instance = new KyrolusSagaInstance { SagaName = "Order", ContextJson = "{}" };
        // Pretend this instance already carries a real, previously-adopted revision, to prove SaveAsync
        // does not touch it on the lost-race path.
        instance.Version = 3;

        (await store.SaveAsync(instance)).ShouldBeFalse();
        instance.Version.ShouldBe(3);
    }

    [Fact(DisplayName = "MartenSagaStore: GetAsync maps a found document back to a KyrolusSagaInstance")]
    public async Task GetAsync_MapsFoundDocument()
    {
        var documentStore = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IQuerySession>();
        documentStore.QuerySession().Returns(session);

        var sagaId = Guid.NewGuid();
        var document = new KyrolusMartenSagaInstanceDocument
        {
            Id = sagaId,
            SagaName = "Order",
            ContextJson = "{\"a\":1}",
            CurrentStepIndex = 2,
            Status = KyrolusSagaStatus.Compensating,
            CorrelationId = "corr-1",
            Version = 4
        };
        session.LoadAsync<KyrolusMartenSagaInstanceDocument>(sagaId, Arg.Any<CancellationToken>()).Returns(document);

        var store = new MartenSagaStore(documentStore);
        var result = await store.GetAsync(sagaId);

        result.ShouldNotBeNull();
        result.Id.ShouldBe(sagaId);
        result.SagaName.ShouldBe("Order");
        result.CurrentStepIndex.ShouldBe(2);
        result.Status.ShouldBe(KyrolusSagaStatus.Compensating);
        result.CorrelationId.ShouldBe("corr-1");
        result.Version.ShouldBe(4);
    }

    [Fact(DisplayName = "MartenSagaStore: GetAsync returns null when no document is found")]
    public async Task GetAsync_ReturnsNull_WhenNotFound()
    {
        var documentStore = Substitute.For<IDocumentStore>();
        var session = Substitute.For<IQuerySession>();
        documentStore.QuerySession().Returns(session);
        session.LoadAsync<KyrolusMartenSagaInstanceDocument>(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((KyrolusMartenSagaInstanceDocument?)null);

        var store = new MartenSagaStore(documentStore);

        (await store.GetAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact(DisplayName = "MartenSagaStore: GetByCorrelationIdAsync rejects a null or whitespace correlation id")]
    public async Task GetByCorrelationIdAsync_RejectsBlankId()
    {
        var store = new MartenSagaStore(Substitute.For<IDocumentStore>());
        await Should.ThrowAsync<ArgumentException>(() => store.GetByCorrelationIdAsync(" "));
    }
}
