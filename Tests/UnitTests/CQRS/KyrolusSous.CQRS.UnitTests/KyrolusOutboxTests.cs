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
}
