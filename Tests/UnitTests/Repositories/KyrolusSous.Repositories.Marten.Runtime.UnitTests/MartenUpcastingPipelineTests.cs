using KyrolusSous.Repositories.Marten.Abstractions.Upcasting;
using KyrolusSous.Repositories.Marten.Runtime.Upcasting;
using Shouldly;
using Xunit;

namespace KyrolusSous.Repositories.Marten.Runtime.UnitTests;

public sealed class MartenUpcastingPipelineTests
{
    public sealed record OrderPlacedV1(Guid OrderId, decimal Amount);
    public sealed record OrderPlacedV2(Guid OrderId, decimal Amount, string Currency);
    public sealed record OrderPlacedV3(Guid OrderId, decimal Amount, string Currency, DateTime PlacedAtUtc);

    private sealed class OrderPlacedV1ToV2Upcaster : KyrolusMartenEventUpcasterBase<OrderPlacedV1, OrderPlacedV2>
    {
        public override OrderPlacedV2 Upcast(OrderPlacedV1 sourceEvent)
            => new(sourceEvent.OrderId, sourceEvent.Amount, "USD");
    }

    private sealed class OrderPlacedV2ToV3Upcaster : KyrolusMartenEventUpcasterBase<OrderPlacedV2, OrderPlacedV3>
    {
        public override OrderPlacedV3 Upcast(OrderPlacedV2 sourceEvent)
            => new(sourceEvent.OrderId, sourceEvent.Amount, sourceEvent.Currency, new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact(DisplayName = "Upcasting: Recursively migrates V1 to V3 schema")]
    public void Upcast_RecursivelyMigratesAcrossVersions()
    {
        var pipeline = new KyrolusMartenUpcastingPipeline([
            new OrderPlacedV1ToV2Upcaster(),
            new OrderPlacedV2ToV3Upcaster()
        ]);

        var orderId = Guid.NewGuid();
        var v1 = new OrderPlacedV1(orderId, 250m);

        var result = pipeline.Upcast(v1);

        result.ShouldBeOfType<OrderPlacedV3>();
        var v3 = (OrderPlacedV3)result;
        v3.OrderId.ShouldBe(orderId);
        v3.Amount.ShouldBe(250m);
        v3.Currency.ShouldBe("USD");
        v3.PlacedAtUtc.ShouldBe(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact(DisplayName = "Upcasting: Returns original event when no upcaster is registered")]
    public void Upcast_ReturnsOriginal_WhenNoUpcasterMatches()
    {
        var pipeline = new KyrolusMartenUpcastingPipeline();
        var original = new OrderPlacedV3(Guid.NewGuid(), 100m, "EUR", DateTime.UtcNow);

        var result = pipeline.Upcast(original);

        result.ShouldBeSameAs(original);
    }
}
