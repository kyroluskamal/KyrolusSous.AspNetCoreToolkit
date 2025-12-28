using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Policy;
using KyrolusSous.Repositories.EF.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace KyrolusSous.Repositories.EF.Generator.UnitTests;

public class KyrolusUnitOfWorkTests
{
    private sealed class FakeObserver : IKyrolusRepositoryObserver
    {
        public List<string> BeforeOps { get; } = [];
        public List<string> AfterOps { get; } = [];
        public List<Exception?> AfterExceptions { get; } = [];

        public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        {
            BeforeOps.Add(operation);
            return Task.CompletedTask;
        }

        public Task OnAfterAsync(string operation, object? payload, TimeSpan? duration = null, Exception? exception = null, CancellationToken cancellationToken = default)
        {
            AfterOps.Add(operation);
            AfterExceptions.Add(exception);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDbContext(DbContextOptions options, Queue<Func<Task<int>>> saveBehaviors) : DbContext(options)
    {
        private readonly Queue<Func<Task<int>>> behaviors = saveBehaviors;
        public int SaveCalls { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            if (behaviors.Count > 0)
            {
                return behaviors.Dequeue().Invoke();
            }
            return Task.FromResult(0);
        }
    }
#pragma warning disable S2094
    private sealed class FakeRepo { }
#pragma warning restore S2094

    private sealed class TxDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<TxEntity> Items => Set<TxEntity>();
    }

    private sealed class TxEntity
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    [Fact(DisplayName = "SaveChangesAsync notifies observer and returns affected rows")]
    public async Task SaveChangesAsync_NotifiesObserver()
    {
        var behaviors = new Queue<Func<Task<int>>>([() => Task.FromResult(2)]);
        var ctx = new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().Options, behaviors);
        var observer = new FakeObserver();
        var uow = new KyrolusRuntimeUnitOfWork<FakeDbContext>(ctx, observer: observer);

        var result = await uow.SaveChangesAsync();

        result.ShouldBe(2);
        observer.BeforeOps.ShouldBe(["SaveChanges"]);
        observer.AfterOps.ShouldBe(["SaveChanges"]);
        observer.AfterExceptions.TrueForAll(e => e is null).ShouldBeTrue();
    }

    [Fact(DisplayName = "SaveChangesAsync forwards exception and notifies observer with error")]
    public async Task SaveChangesAsync_PropagatesException()
    {
        var behaviors = new Queue<Func<Task<int>>>([() => throw new InvalidOperationException("boom")]);
        var ctx = new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().Options, behaviors);
        var observer = new FakeObserver();
        var uow = new KyrolusRuntimeUnitOfWork<FakeDbContext>(ctx, observer: observer);

        await Should.ThrowAsync<InvalidOperationException>(() => uow.SaveChangesAsync());
        observer.BeforeOps.ShouldBe(["SaveChanges"]);
        observer.AfterOps.ShouldBe(["SaveChanges"]);
        observer.AfterExceptions.Single().ShouldNotBeNull();
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync retries on concurrency and succeeds")]
    public async Task SaveChangesWithRetryAsync_RetriesThenSucceeds()
    {
        var behaviors = new Queue<Func<Task<int>>>([
            () => throw new DbUpdateConcurrencyException("first"),
            () => Task.FromResult(1)
        ]);
        var ctx = new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().Options, behaviors);
        var observer = new FakeObserver();
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 1 };
        var uow = new KyrolusRuntimeUnitOfWork<FakeDbContext>(ctx, policy, observer);

        var result = await uow.SaveChangesWithRetryAsync();

        ctx.SaveCalls.ShouldBe(2);
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        observer.BeforeOps.Count.ShouldBe(2); // each attempt
        observer.AfterOps.Count.ShouldBe(1);  // only on success
    }

    [Fact(DisplayName = "SaveChangesWithRetryAsync returns conflict when all attempts fail")]
    public async Task SaveChangesWithRetryAsync_ReturnsConflictWhenAllFail()
    {
        var behaviors = new Queue<Func<Task<int>>>([
            () => throw new DbUpdateConcurrencyException("first"),
            () => throw new DbUpdateConcurrencyException("second")
        ]);
        var ctx = new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().Options, behaviors);
        var observer = new FakeObserver();
        var policy = new KyrolusRepositoryPolicy { ConcurrencyRetryCount = 1 };
        var uow = new KyrolusRuntimeUnitOfWork<FakeDbContext>(ctx, policy, observer);

        var result = await uow.SaveChangesWithRetryAsync();

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.ConcurrencyConflict);
        result.Concurrency?.RetryCount.ShouldBe(1);
        observer.AfterOps.Count.ShouldBe(0); // no success callback when conflict
    }

    [Fact(DisplayName = "ExecuteAsync without transaction runs work then save")]
    public async Task ExecuteAsync_NoTransaction()
    {
        var behaviors = new Queue<Func<Task<int>>>([() => Task.FromResult(3)]);
        var ctx = new FakeDbContext(new DbContextOptionsBuilder<FakeDbContext>().Options, behaviors);
        var uow = new KyrolusRuntimeUnitOfWork<FakeDbContext>(ctx);

        var workCalled = false;
        var result = await uow.ExecuteAsync(() =>
        {
            workCalled = true;
            return Task.CompletedTask;
        }, useTransaction: false);

        workCalled.ShouldBeTrue();
        ctx.SaveCalls.ShouldBe(1);
        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
    }

    [Fact(DisplayName = "Dispose clears cache and marks disposed")]
    public void Dispose_ClearsCache()
    {
        var created = 0;
        var factory = new Func<Type, object?>(_ => { created++; return new FakeRepo(); });
        var uow = new KyrolusRuntimeUnitOfWork<DbContext>(new DbContext(new DbContextOptions<DbContext>()), repositoryFactory: factory);

        uow.GetRepository<FakeRepo>();
        created.ShouldBe(1);
        uow.Dispose();
        Should.NotThrow(() => uow.Dispose()); // idempotent
    }

    [Fact(DisplayName = "DisposeAsync clears cache and marks disposed")]
    public async Task DisposeAsync_ClearsCache()
    {
        var uow = new KyrolusRuntimeUnitOfWork<DbContext>(new DbContext(new DbContextOptions<DbContext>()));
        await uow.DisposeAsync();
        await Should.NotThrowAsync(() => uow.DisposeAsync().AsTask());
    }

    [Fact(DisplayName = "ExecuteAsync with transaction commits changes (SQLite in-memory)")]
    public async Task ExecuteAsync_WithTransaction_Commits()
    {
        await using var connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<TxDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var setup = new TxDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
        }

        await using var ctx = new TxDbContext(options);
        var uow = new KyrolusRuntimeUnitOfWork<TxDbContext>(ctx);

        var result = await uow.ExecuteAsync(async () =>
        {
            ctx.Items.Add(new TxEntity { Id = 1, Name = "one" });
            await Task.CompletedTask;
        }, useTransaction: true);

        result.Status.ShouldBe(KyrolusRepositoryOperationStatus.Success);
        (await ctx.Items.CountAsync()).ShouldBe(1);
    }

    [Fact(DisplayName = "GetRepository uses factory and caches instances")]
    public void GetRepository_UsesFactoryAndCache()
    {
        var created = 0;
        var factory = new Func<Type, object?>(t =>
        {
            created++;
            return new FakeRepo();
        });
        var uow = new KyrolusRuntimeUnitOfWork<DbContext>(new DbContext(new DbContextOptions<DbContext>()), repositoryFactory: factory);

        var first = uow.GetRepository<FakeRepo>();
        var second = uow.GetRepository<FakeRepo>();

        first.ShouldBeSameAs(second);
        created.ShouldBe(1);
    }

    [Fact(DisplayName = "GetRepository resolves from service provider when factory returns null")]
    public void GetRepository_UsesServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<FakeRepo>();
        var provider = services.BuildServiceProvider();

        var uow = new KyrolusRuntimeUnitOfWork<DbContext>(new DbContext(new DbContextOptions<DbContext>()), serviceProvider: provider);
        var repo = uow.GetRepository<FakeRepo>();

        repo.ShouldNotBeNull();
    }

    [Fact(DisplayName = "GetRepository throws when nothing registered")]
    public void GetRepository_ThrowsWhenMissing()
    {
        var uow = new KyrolusRuntimeUnitOfWork<DbContext>(new DbContext(new DbContextOptions<DbContext>()));
        Should.Throw<InvalidOperationException>(() => uow.GetRepository<FakeRepo>());
    }

    [Fact(DisplayName = "GetRepository with name returns same instance (name ignored)")]
    public void GetRepository_WithName_ReturnsInstance()
    {
        var uow = new KyrolusRuntimeUnitOfWork<DbContext>(new DbContext(new DbContextOptions<DbContext>()), repositoryFactory: _ => new FakeRepo());
        var repo1 = uow.GetRepository<FakeRepo>("anything");
        var repo2 = uow.GetRepository<FakeRepo>();

        repo1.ShouldNotBeNull();
        repo1.ShouldBeSameAs(repo2);
    }
}
