using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;
using KyrolusSous.Repositories.Marten.Abstractions.Outbox;
using KyrolusSous.Repositories.Marten.Runtime.Outbox;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenOutboxUnitTests
{
    public sealed record UserRegisteredIntegrationEvent(Guid UserId, string Email);

    [Fact(DisplayName = "Outbox: AddOutboxMessage stores message into session")]
    public void AddOutboxMessage_StoresMessageInSession()
    {
        var session = Substitute.For<IDocumentSession>();
        var evt = new UserRegisteredIntegrationEvent(Guid.NewGuid(), "user@example.com");

        session.AddOutboxMessage(evt);

        session.Received(1).Store(Arg.Is<KyrolusMartenOutboxMessage>(msg =>
            msg.Id != Guid.Empty &&
            msg.EventType.Contains(nameof(UserRegisteredIntegrationEvent)) &&
            msg.Payload.Contains("user@example.com") &&
            !msg.Processed));
    }

    private sealed class TestUowWithOutbox : IKyrolusMartenUnitOfWork<IDocumentSession>, IKyrolusMartenOutboxStore
    {
        public List<KyrolusMartenOutboxMessage> Enqueued { get; } = [];

        public TRepo GetRepository<TRepo>() where TRepo : class => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public Task EnqueueAsync(KyrolusMartenOutboxMessage message, CancellationToken cancellationToken = default)
        {
            Enqueued.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<KyrolusMartenOutboxMessage>> GetPendingMessagesAsync(int batchSize = 100, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<KyrolusMartenOutboxMessage>>(Enqueued);

        public Task MarkProcessedAsync(Guid messageId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    [Fact(DisplayName = "Outbox: AddOutboxMessageAsync enqueues on outbox-capable unit of work")]
    public async Task AddOutboxMessageAsync_EnqueuesOnOutboxStore()
    {
        var uow = new TestUowWithOutbox();
        var evt = new UserRegisteredIntegrationEvent(Guid.NewGuid(), "outbox@example.com");

        await uow.AddOutboxMessageAsync(evt);

        uow.Enqueued.Count.ShouldBe(1);
        uow.Enqueued[0].Payload.ShouldContain("outbox@example.com");
    }

    private sealed class PlainUow : IKyrolusMartenUnitOfWork<IDocumentSession>
    {
        public TRepo GetRepository<TRepo>() where TRepo : class => throw new NotImplementedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => Task.FromResult(1);
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact(DisplayName = "Outbox: AddOutboxMessageAsync throws InvalidOperationException if unit of work is not an outbox store")]
    public async Task AddOutboxMessageAsync_ThrowsIfNotOutboxStore()
    {
        var uow = new PlainUow();
        var evt = new UserRegisteredIntegrationEvent(Guid.NewGuid(), "throw@example.com");

        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await uow.AddOutboxMessageAsync(evt);
        });
    }
}
