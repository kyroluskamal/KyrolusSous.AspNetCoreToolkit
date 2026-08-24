using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Outbox;
using KyrolusSous.Repositories.EF.Runtime.Outbox;
using KyrolusSous.Repositories.EF.Runtime.Temporal;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.EF.Runtime.UnitTests;

public sealed class OutboxAndTemporalTests
{
    private sealed record OrderCreatedEvent(Guid OrderId, decimal Amount);

    private sealed class MockOutboxUnitOfWork : IKyrolusUnitOfWork, IKyrolusOutboxStore
    {
        public List<KyrolusOutboxMessage> Enqueued { get; } = [];

        public Task EnqueueAsync(KyrolusOutboxMessage message, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(message);
            return Task.CompletedTask;
        }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);

        public Task<RepositoryOperationResult<int>> SaveChangesWithRetryAsync(string? rowVersionPropertyName = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryOperationResult<int>(KyrolusRepositoryOperationStatus.Success, 1));

        public Task<RepositoryOperationResult<int>> ExecuteAsync(Func<Task> work, bool useTransaction = true, string? rowVersionPropertyName = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new RepositoryOperationResult<int>(KyrolusRepositoryOperationStatus.Success, 1));

        public TRepo GetRepository<TRepo>() where TRepo : class => default!;
        public TRepo? GetRepository<TRepo>(string name) where TRepo : class => default;

        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class TemporalEntity
    {
        public int Id { get; set; }
        public string Data { get; set; } = string.Empty;
    }

    private sealed class TemporalDbContext(DbContextOptions<TemporalDbContext> options) : DbContext(options)
    {
        public DbSet<TemporalEntity> Entities => Set<TemporalEntity>();
    }

    [Fact(DisplayName = "OutboxExtensions: Enqueues serialized domain events accurately")]
    public async Task OutboxExtensions_EnqueuesDomainEvent()
    {
        var uow = new MockOutboxUnitOfWork();
        var domainEvent = new OrderCreatedEvent(Guid.NewGuid(), 250.75m);

        await uow.AddOutboxMessageAsync(domainEvent);

        uow.Enqueued.Count.ShouldBe(1);
        var msg = uow.Enqueued[0];
        msg.EventType.ShouldContain(nameof(OrderCreatedEvent));
        msg.Payload.ShouldContain("250.75");
        msg.OccurredOnUtc.ShouldNotBe(default);
        msg.ProcessedOnUtc.ShouldBeNull();
    }

    [Fact(DisplayName = "TemporalExtensions: AsOf and Between gracefully pass through when provider is not SqlServer")]
    public void TemporalExtensions_ProviderAgnosticGracefulFallback()
    {
        var options = new DbContextOptionsBuilder<TemporalDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        using var context = new TemporalDbContext(options);
        var asOf = context.Entities.AsOf(DateTime.UtcNow.AddDays(-1));
        asOf.ShouldNotBeNull();

        var between = context.Entities.Between(DateTime.UtcNow.AddDays(-5), DateTime.UtcNow);
        between.ShouldNotBeNull();
    }
}
