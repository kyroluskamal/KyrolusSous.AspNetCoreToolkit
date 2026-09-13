using KyrolusSous.CQRS.EF.Saga;
using KyrolusSous.CQRS.Saga;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

/// <summary>
/// Covers <see cref="EfSagaStore{TDbContext}"/> against a real relational provider (SQLite, in-memory
/// via a kept-open connection) rather than the EF InMemory provider, since
/// <see cref="EfSagaStore{TDbContext}.SaveAsync"/>'s version-gated update relies on
/// <c>ExecuteUpdateAsync</c>, which the InMemory provider does not support.
/// </summary>
public sealed class EfSagaStoreTests : IDisposable
{
    public sealed class SagaTestDbContext(DbContextOptions<SagaTestDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyKyrolusSagaStore();
        }
    }

    // SQLite's ":memory:" database only lives as long as the connection that opened it stays open, so
    // every ServiceProvider/DbContext this test builds shares this ONE connection - that is what makes
    // "a brand-new EfSagaStore built from a brand-new ServiceProvider still sees what an earlier one
    // wrote" a meaningful simulation of two different application processes talking to the same durable
    // database, rather than two objects secretly sharing in-memory state.
    private readonly SqliteConnection _connection;

    public EfSagaStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var provider = BuildProvider();
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<SagaTestDbContext>().Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddDbContext<SagaTestDbContext>(o => o.UseSqlite(_connection));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Builds a store backed by its own, brand-new <see cref="ServiceProvider"/> (and therefore its own
    /// <see cref="IServiceScopeFactory"/>) - simulating a distinct application instance - while still
    /// pointing at the shared <see cref="_connection"/>. The caller owns disposing <paramref name="provider"/>.
    /// </summary>
    private EfSagaStore<SagaTestDbContext> CreateStore(out ServiceProvider provider)
    {
        provider = BuildProvider();
        return new EfSagaStore<SagaTestDbContext>(provider.GetRequiredService<IServiceScopeFactory>());
    }

    [Fact(DisplayName = "EfSagaStore: SaveAsync on a brand-new instance succeeds, bumps Version to 1, and persists it")]
    public async Task SaveAsync_BrandNewInstance_SucceedsAndPersists()
    {
        var store = CreateStore(out var provider);
        using var _ = provider;

        var instance = new KyrolusSagaInstance { SagaName = "Order", ContextJson = "{\"a\":1}", CorrelationId = "corr-1" };

        (await store.SaveAsync(instance)).ShouldBeTrue();
        instance.Version.ShouldBe(1);

        var fetched = await store.GetAsync(instance.Id);
        fetched.ShouldNotBeNull();
        fetched.Id.ShouldBe(instance.Id);
        fetched.SagaName.ShouldBe("Order");
        fetched.ContextJson.ShouldBe("{\"a\":1}");
        fetched.CorrelationId.ShouldBe("corr-1");
        fetched.Version.ShouldBe(1);
        fetched.Status.ShouldBe(KyrolusSagaStatus.Running);
    }

    [Fact(DisplayName = "EfSagaStore: a second SaveAsync with a stale Version returns false and does not overwrite the stored row")]
    public async Task SaveAsync_StaleVersion_ReturnsFalse_DoesNotOverwrite()
    {
        var store = CreateStore(out var provider);
        using var _ = provider;

        var instance = new KyrolusSagaInstance { SagaName = "Order", ContextJson = "{}" };
        (await store.SaveAsync(instance)).ShouldBeTrue(); // Version 0 -> 1

        var staleReader = await store.GetAsync(instance.Id); // independent snapshot at Version 1
        staleReader.ShouldNotBeNull();

        instance.CurrentStepIndex = 1;
        (await store.SaveAsync(instance)).ShouldBeTrue(); // Version 1 -> 2, still the only real writer

        staleReader.CurrentStepIndex = 99;
        (await store.SaveAsync(staleReader)).ShouldBeFalse(); // staleReader still carries Version 1; store is at 2

        var current = await store.GetAsync(instance.Id);
        current.ShouldNotBeNull();
        current.Version.ShouldBe(2);
        current.CurrentStepIndex.ShouldBe(1); // the rejected write's CurrentStepIndex = 99 never applied
    }

    [Fact(DisplayName = "EfSagaStore: SaveAsync for a brand-new Version-0 instance whose id already exists loses the race and returns false")]
    public async Task SaveAsync_NewVersionButIdAlreadyExists_ReturnsFalse()
    {
        var store = CreateStore(out var provider);
        using var _ = provider;

        var first = new KyrolusSagaInstance { SagaName = "Order", ContextJson = "{}" };
        (await store.SaveAsync(first)).ShouldBeTrue();

        // Same id, but this caller still thinks it is Version 0 (never read the row that already exists).
        var conflicting = new KyrolusSagaInstance { Id = first.Id, SagaName = "Order", ContextJson = "{}" };
        (await store.SaveAsync(conflicting)).ShouldBeFalse();
        conflicting.Version.ShouldBe(0); // left untouched, per the interface's contract
    }

    [Fact(DisplayName = "EfSagaStore: GetByCorrelationIdAsync finds a stored instance by its correlation id, or returns null when none matches")]
    public async Task GetByCorrelationIdAsync_FindsByCorrelationIdOrReturnsNull()
    {
        var store = CreateStore(out var provider);
        using var _ = provider;

        var instance = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}", CorrelationId = "corr-x" };
        await store.SaveAsync(instance);

        var found = await store.GetByCorrelationIdAsync("corr-x");
        found.ShouldNotBeNull();
        found.Id.ShouldBe(instance.Id);

        (await store.GetByCorrelationIdAsync("does-not-exist")).ShouldBeNull();
    }

    [Fact(DisplayName = "EfSagaStore: GetIncompleteAsync returns only Running/Compensating instances, not Completed/Compensated/Failed ones")]
    public async Task GetIncompleteAsync_ReturnsOnlyRunningOrCompensating()
    {
        var store = CreateStore(out var provider);
        using var _ = provider;

        var running = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}", Status = KyrolusSagaStatus.Running };
        var compensating = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}", Status = KyrolusSagaStatus.Compensating };
        var completed = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}", Status = KyrolusSagaStatus.Completed };
        var failed = new KyrolusSagaInstance { SagaName = "S", ContextJson = "{}", Status = KyrolusSagaStatus.Failed };

        foreach (var instance in new[] { running, compensating, completed, failed })
            await store.SaveAsync(instance);

        var incomplete = await store.GetIncompleteAsync();

        incomplete.Select(i => i.Id).ShouldBe([running.Id, compensating.Id], ignoreOrder: true);
    }

    // --- End-to-end: proves the actual point of this feature ---------------------------------------

    public sealed record TestSagaContext
    {
        public List<string> Log { get; set; } = [];
    }

    private sealed class RecordingStep(string name) : IKyrolusSagaStep<TestSagaContext>
    {
        public string Name => name;

        public Task ExecuteAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            context.Log.Add($"execute:{name}");
            return Task.CompletedTask;
        }

        public Task CompensateAsync(TestSagaContext context, CancellationToken cancellationToken)
        {
            context.Log.Add($"compensate:{name}");
            return Task.CompletedTask;
        }
    }

    private sealed class TestSaga(IReadOnlyList<IKyrolusSagaStep<TestSagaContext>> steps) : KyrolusSagaDefinition<TestSagaContext>
    {
        public override string SagaName => "EfTestSaga";
        protected override IReadOnlyList<IKyrolusSagaStep<TestSagaContext>> Steps { get; } = steps;
    }

    [Fact(DisplayName = "EfSagaStore + KyrolusSagaCoordinator: a saga started by one store/provider survives a simulated process restart and is resumed to completion by an unrelated one reading the same database")]
    public async Task Saga_EfSagaStore_SurvivesSimulatedProcessRestart_ResumesAndCompletes()
    {
        var saga = new TestSaga([new RecordingStep("Reserve"), new RecordingStep("Charge"), new RecordingStep("Ship")]);

        // "Process 1": starts the saga through a real EfSagaStore/coordinator pair, but never gets to
        // run Charge/Ship - simulate the crash by seeding the exact state a real StartAsync run would
        // have persisted right after Reserve completed (the same technique KyrolusSagaTests uses for
        // InMemorySagaStore's own crash-resume coverage), so the persisted row is what a genuine
        // mid-flight crash would have left behind.
        Guid sagaId;
        {
            var store1 = CreateStore(out var provider1);
            using var _ = provider1;

            var crashedInstance = new KyrolusSagaInstance
            {
                SagaName = saga.SagaName,
                ContextJson = saga.SerializeContext(new TestSagaContext { Log = ["execute:Reserve"] }),
                CurrentStepIndex = 1,
                Status = KyrolusSagaStatus.Running,
                StepCountAtStart = saga.StepCount,
                StepSignatureAtStart = saga.StepSignature
            };
            (await store1.SaveAsync(crashedInstance)).ShouldBeTrue();
            sagaId = crashedInstance.Id;

            // provider1 (and therefore every DbContext/scope it ever created) is disposed here -
            // nothing from "process 1" survives past this block except what actually reached SQLite.
        }

        // "Process 2": an entirely separate ServiceProvider, EfSagaStore, registry and coordinator -
        // sharing no object with "process 1" except the same underlying SQLite connection/database.
        var store2 = CreateStore(out var provider2);
        using var __ = provider2;
        var registry2 = new KyrolusSagaDefinitionRegistry([saga]);
        var coordinator2 = new KyrolusSagaCoordinator(store2, registry2);

        var resumedCount = await coordinator2.ResumeIncompleteAsync(CancellationToken.None);

        resumedCount.ShouldBe(1);
        var final = await store2.GetAsync(sagaId);
        final.ShouldNotBeNull();
        final.Status.ShouldBe(KyrolusSagaStatus.Completed);

        var finalContext = (TestSagaContext)saga.DeserializeContext(final.ContextJson);
        // "execute:Reserve" appears exactly once (it ran under "process 1", was durably persisted, and
        // was NOT re-run) followed by Charge and Ship, which only "process 2" ever executed.
        finalContext.Log.ShouldBe(["execute:Reserve", "execute:Charge", "execute:Ship"]);
    }
}
