using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Marten.Behaviors;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusMartenCqrsTests
{
    public sealed record TestMartenCommand(Guid Id) : IKyrolusCommand<bool>, IKyrolusDomainEventSource
    {
        private readonly List<object> _events = [];
        public IReadOnlyCollection<object> DomainEvents => _events.AsReadOnly();
        public void AddEvent(object @event) => _events.Add(@event);
        public void ClearDomainEvents() => _events.Clear();
    }

    public sealed record TestMartenEvent(Guid Id);

    [Fact(DisplayName = "MartenTransactionBehavior: Calls SaveChangesAsync on IDocumentSession")]
    public async Task MartenTransactionBehavior_CallsSaveChangesAsync()
    {
        var session = Substitute.For<IDocumentSession>();
        var behavior = new KyrolusMartenTransactionBehavior<TestMartenCommand, bool>(session);

        var cmd = new TestMartenCommand(Guid.NewGuid());
        var result = await behavior.Handle(cmd, ct => Task.FromResult(true), CancellationToken.None);

        result.ShouldBeTrue();
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "MartenDomainEventsBehavior: Dispatches domain events on command success")]
    public async Task MartenDomainEventsBehavior_DispatchesEvents()
    {
        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        var behavior = new KyrolusMartenDomainEventsDispatchBehavior<TestMartenCommand, bool>(publisher);

        var cmd = new TestMartenCommand(Guid.NewGuid());
        cmd.AddEvent(new TestMartenEvent(cmd.Id));

        var result = await behavior.Handle(cmd, ct => Task.FromResult(true), CancellationToken.None);

        result.ShouldBeTrue();
        cmd.DomainEvents.Count.ShouldBe(0);
        await publisher.Received(1).PublishAsync(Arg.Is<object>(e => e is TestMartenEvent), Arg.Any<CancellationToken>());
    }
}
