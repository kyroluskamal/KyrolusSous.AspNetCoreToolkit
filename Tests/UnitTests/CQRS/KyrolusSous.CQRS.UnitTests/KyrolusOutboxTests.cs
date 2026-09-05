using System.Text.Json;
using KyrolusSous.CQRS.Abstractions.Outbox;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public class KyrolusOutboxTests
{
    public sealed record OrderPlacedIntegrationEvent(string OrderId, decimal Amount) : IKyrolusNotification;

    private sealed class FakePublisher : IKyrolusMediatorPublisher
    {
        public List<object> PublishedEvents { get; } = [];

        public Task PublishAsync(IKyrolusNotification notification, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishAsync(IKyrolusNotification notification, IKyrolusNotificationPublishStrategy? strategy, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }

        public Task PublishAsync(object notification, CancellationToken cancellationToken = default)
        {
            PublishedEvents.Add(notification);
            return Task.CompletedTask;
        }
    }

    [Fact(DisplayName = "Outbox processor should dispatch pending messages and mark processed")]
    public async Task Outbox_processor_should_dispatch_pending_messages_and_mark_processed()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var publisher = new FakePublisher();
        var processor = new KyrolusOutboxProcessor(store, publisher);

        var domainEvent = new OrderPlacedIntegrationEvent("ord-100", 250m);
        var msg = new KyrolusOutboxMessage
        {
            Id = Guid.NewGuid(),
            CorrelationId = "corr-1",
            EventType = typeof(OrderPlacedIntegrationEvent).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(domainEvent),
            Status = KyrolusOutboxMessageStatus.Pending
        };

        await store.SaveAsync(msg);

        var count = await processor.ProcessPendingMessagesAsync(10);

        count.ShouldBe(1);
        publisher.PublishedEvents.Count.ShouldBe(1);
        publisher.PublishedEvents.First().ShouldBeOfType<OrderPlacedIntegrationEvent>();

        var all = store.AllMessages.ToList();
        all[0].Status.ShouldBe(KyrolusOutboxMessageStatus.Processed);
        all[0].ProcessedOnUtc.ShouldNotBeNull();
        all[0].Error.ShouldBeNull();
    }

    [Fact(DisplayName = "Outbox processor should handle invalid event type and mark failed")]
    public async Task Outbox_processor_should_handle_invalid_event_type_and_mark_failed()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var publisher = new FakePublisher();
        var processor = new KyrolusOutboxProcessor(store, publisher);

        var msg = new KyrolusOutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "NonExistent.EventType, SomeAssembly",
            Payload = "{}",
            Status = KyrolusOutboxMessageStatus.Pending
        };

        await store.SaveAsync(msg);

        var count = await processor.ProcessPendingMessagesAsync(10);

        count.ShouldBe(0);
        publisher.PublishedEvents.ShouldBeEmpty();

        var all = store.AllMessages.ToList();
        all[0].Status.ShouldBe(KyrolusOutboxMessageStatus.Failed);
        all[0].Error.ShouldNotBeNull();
        all[0].Error!.ShouldContain("not in the outbox type registry's allow-list");
    }

    [Fact(DisplayName = "Outbox message becomes DeadLettered after MaxRetryCount consecutive failures")]
    public async Task Outbox_message_becomes_dead_lettered_after_max_retry_count()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var msg = new KyrolusOutboxMessage { EventType = "Some.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        await store.SaveAsync(msg);

        for (var i = 0; i < KyrolusOutboxLimits.MaxRetryCount - 1; i++)
        {
            await store.MarkFailedAsync(msg.Id, "transient error");
            store.AllMessages.Single().Status.ShouldBe(KyrolusOutboxMessageStatus.Failed);
        }

        await store.MarkFailedAsync(msg.Id, "final error");

        var stored = store.AllMessages.Single();
        stored.Status.ShouldBe(KyrolusOutboxMessageStatus.DeadLettered);
        stored.RetryCount.ShouldBe(KyrolusOutboxLimits.MaxRetryCount);
        stored.Error.ShouldBe("final error");
    }

    [Fact(DisplayName = "GetDeadLetteredAsync returns exactly the dead-lettered messages")]
    public async Task GetDeadLetteredAsync_returns_only_dead_lettered_messages()
    {
        var store = new KyrolusInMemoryOutboxStore();

        var pending = new KyrolusOutboxMessage { EventType = "Pending.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        var deadLettered = new KyrolusOutboxMessage { EventType = "Dead.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        var processed = new KyrolusOutboxMessage { EventType = "Processed.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Processed };

        await store.SaveAsync(pending);
        await store.SaveAsync(deadLettered);
        await store.SaveAsync(processed);

        for (var i = 0; i < KyrolusOutboxLimits.MaxRetryCount; i++)
        {
            await store.MarkFailedAsync(deadLettered.Id, "error");
        }

        var result = await store.GetDeadLetteredAsync();

        result.Count.ShouldBe(1);
        result[0].Id.ShouldBe(deadLettered.Id);
        result[0].Status.ShouldBe(KyrolusOutboxMessageStatus.DeadLettered);
    }

    [Fact(DisplayName = "RequeueAsync resets a dead-lettered message to Pending with RetryCount zeroed")]
    public async Task RequeueAsync_resets_dead_lettered_message_to_pending()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var msg = new KyrolusOutboxMessage { EventType = "Some.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        await store.SaveAsync(msg);

        for (var i = 0; i < KyrolusOutboxLimits.MaxRetryCount; i++)
        {
            await store.MarkFailedAsync(msg.Id, "error");
        }

        store.AllMessages.Single().Status.ShouldBe(KyrolusOutboxMessageStatus.DeadLettered);

        var requeued = await store.RequeueAsync(msg.Id);

        requeued.ShouldBeTrue();
        var stored = store.AllMessages.Single();
        stored.Status.ShouldBe(KyrolusOutboxMessageStatus.Pending);
        stored.RetryCount.ShouldBe(0);
        stored.Error.ShouldBeNull();
    }

    [Fact(DisplayName = "RequeueAsync returns false for a message that is not DeadLettered")]
    public async Task RequeueAsync_returns_false_when_message_not_dead_lettered()
    {
        var store = new KyrolusInMemoryOutboxStore();

        var pendingMsg = new KyrolusOutboxMessage { EventType = "Pending.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        var processedMsg = new KyrolusOutboxMessage { EventType = "Processed.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Processed };
        await store.SaveAsync(pendingMsg);
        await store.SaveAsync(processedMsg);

        (await store.RequeueAsync(pendingMsg.Id)).ShouldBeFalse();
        (await store.RequeueAsync(processedMsg.Id)).ShouldBeFalse();
        (await store.RequeueAsync(Guid.NewGuid())).ShouldBeFalse();

        store.AllMessages.Single(m => m.Id == pendingMsg.Id).Status.ShouldBe(KyrolusOutboxMessageStatus.Pending);
        store.AllMessages.Single(m => m.Id == processedMsg.Id).Status.ShouldBe(KyrolusOutboxMessageStatus.Processed);
    }

    [Fact(DisplayName = "GetPendingAsync clamps a batchSize above MaxBatchSize instead of passing it through unclamped")]
    public async Task GetPendingAsync_BatchSizeAboveMax_IsClamped()
    {
        var store = new KyrolusInMemoryOutboxStore();
        for (var i = 0; i < 3; i++)
        {
            await store.SaveAsync(new KyrolusOutboxMessage { EventType = $"Event.{i}", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending });
        }

        // Requesting far more than MaxBatchSize must not throw and must not be honored verbatim -
        // clamping happens silently rather than rejecting the call, since the caller only asked for
        // "up to" this many messages.
        var pending = await store.GetPendingAsync(KyrolusOutboxLimits.MaxBatchSize + 1000);

        pending.Count.ShouldBe(3);
    }

    [Fact(DisplayName = "GetDeadLetteredAsync clamps a batchSize above MaxBatchSize instead of passing it through unclamped")]
    public async Task GetDeadLetteredAsync_BatchSizeAboveMax_IsClamped()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var msg = new KyrolusOutboxMessage { EventType = "Some.Event", Payload = "{}", Status = KyrolusOutboxMessageStatus.Pending };
        await store.SaveAsync(msg);
        for (var i = 0; i < KyrolusOutboxLimits.MaxRetryCount; i++)
        {
            await store.MarkFailedAsync(msg.Id, "error");
        }

        var deadLettered = await store.GetDeadLetteredAsync(KyrolusOutboxLimits.MaxBatchSize + 1000);

        deadLettered.Count.ShouldBe(1);
    }

    [Fact(DisplayName = "ProcessPendingMessagesAsync clamps a batchSize above MaxBatchSize before querying the store")]
    public async Task ProcessPendingMessagesAsync_BatchSizeAboveMax_IsClamped()
    {
        var store = new KyrolusInMemoryOutboxStore();
        var publisher = new FakePublisher();
        var processor = new KyrolusOutboxProcessor(store, publisher);

        var domainEvent = new OrderPlacedIntegrationEvent("ord-1", 10m);
        await store.SaveAsync(new KyrolusOutboxMessage
        {
            EventType = typeof(OrderPlacedIntegrationEvent).AssemblyQualifiedName!,
            Payload = JsonSerializer.Serialize(domainEvent),
            Status = KyrolusOutboxMessageStatus.Pending
        });

        // Must not throw despite the wildly oversized batchSize, and must still process the one
        // available message rather than being rejected outright.
        var count = await processor.ProcessPendingMessagesAsync(KyrolusOutboxLimits.MaxBatchSize * 10);

        count.ShouldBe(1);
    }
}
