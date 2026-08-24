using KyrolusSous.Repositories.Marten.Runtime.EventStore;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenEventStoreUnitTests
{
    public sealed record OrderPlaced(Guid OrderId, decimal Amount);
    public sealed record OrderPaid(Guid OrderId, DateTime PaidAt);

    [Fact(DisplayName = "EventStore: Appends events without expected version and saves session")]
    public async Task AppendEventsAsync_WithoutExpectedVersion_AppendsAndSaves()
    {
        var session = Substitute.For<IDocumentSession>();

        var store = new KyrolusMartenEventStore(session);
        var streamId = Guid.NewGuid();
        var events = new object[] { new OrderPlaced(streamId, 150m) };

        await store.AppendEventsAsync(streamId, events);

        session.Events.Received(1).Append(streamId.ToString(), Arg.Is<object[]>(x => x.Length == 1));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "EventStore: Appends events with expected version for optimistic concurrency")]
    public async Task AppendEventsAsync_WithExpectedVersion_AppendsSpecificVersion()
    {
        var session = Substitute.For<IDocumentSession>();

        var store = new KyrolusMartenEventStore(session);
        var streamId = Guid.NewGuid();
        var events = new object[] { new OrderPaid(streamId, DateTime.UtcNow) };

        await store.AppendEventsAsync(streamId, events, expectedVersion: 3);

        session.Events.Received(1).Append(streamId.ToString(), 3L, Arg.Is<object[]>(x => x.Length == 1));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
