using KyrolusSous.Repositories.Marten.Runtime.Saga;
using Marten;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenSagaCoordinatorUnitTests
{
    public sealed class OrderState
    {
        public Guid OrderId { get; set; }
        public string Status { get; set; } = "Started";
    }

    [Fact(DisplayName = "SagaCoordinator: Stores saga envelope and commits session")]
    public async Task StartAsync_StoresEnvelopeAndSaves()
    {
        var session = Substitute.For<IDocumentSession>();
        var coordinator = new KyrolusMartenSagaCoordinator(session);

        var initialState = new OrderState { OrderId = Guid.NewGuid(), Status = "Processing" };

        var sagaId = await coordinator.StartAsync(initialState);

        sagaId.ShouldNotBe(Guid.Empty);
        session.Received(1).Store(Arg.Is<KyrolusMartenSagaEnvelope>(e => e.Id == sagaId && !e.Completed));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "SagaCoordinator: Completes saga envelope and updates state")]
    public async Task CompleteAsync_UpdatesEnvelopeCompletedFlag()
    {
        var session = Substitute.For<IDocumentSession>();
        var sagaId = Guid.NewGuid();
        var existingEnvelope = new KyrolusMartenSagaEnvelope
        {
            Id = sagaId,
            Completed = false,
            Type = typeof(OrderState).AssemblyQualifiedName,
            Payload = "{}"
        };

        session.LoadAsync<KyrolusMartenSagaEnvelope>(sagaId, Arg.Any<CancellationToken>())
            .Returns(existingEnvelope);

        var coordinator = new KyrolusMartenSagaCoordinator(session);
        var completed = await coordinator.CompleteAsync(sagaId);

        completed.ShouldBeTrue();
        session.Received(1).Store(Arg.Is<KyrolusMartenSagaEnvelope>(e => e.Id == sagaId && e.Completed));
        await session.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
