using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.EF.Behaviors;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusDomainEventsDispatchBehaviorTests
{
    public sealed record OrderPlacedEvent(Guid OrderId);

    public sealed class TestEntity : IDomainEventSource
    {
        public Guid Id { get; set; }
        private readonly List<object> _domainEvents = [];
        public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

        public void AddDomainEvent(object @event) => _domainEvents.Add(@event);
        public void ClearDomainEvents() => _domainEvents.Clear();
    }

    public sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestEntity> TestEntities => Set<TestEntity>();
    }

    public sealed record PlaceOrderCommand(Guid OrderId) : IKyrolusCommand<bool>;

    [Fact(DisplayName = "DomainEvents: Collects and dispatches domain events on command success")]
    public async Task DomainEvents_DispatchesAndClearsEvents()
    {
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new TestDbContext(options);
        var entity = new TestEntity { Id = Guid.NewGuid() };
        var domainEvent = new OrderPlacedEvent(entity.Id);
        entity.AddDomainEvent(domainEvent);

        dbContext.TestEntities.Add(entity);

        var publisher = Substitute.For<IKyrolusMediatorPublisher>();
        var behavior = new KyrolusDomainEventsDispatchBehavior<PlaceOrderCommand, bool, TestDbContext>(publisher, dbContext);

        var command = new PlaceOrderCommand(entity.Id);
        var result = await behavior.Handle(command, ct => Task.FromResult(true), CancellationToken.None);

        result.ShouldBeTrue();
        entity.DomainEvents.Count.ShouldBe(0);
        await publisher.Received(1).PublishAsync(Arg.Is<object>(e => e is OrderPlacedEvent), Arg.Any<CancellationToken>());
    }
}
