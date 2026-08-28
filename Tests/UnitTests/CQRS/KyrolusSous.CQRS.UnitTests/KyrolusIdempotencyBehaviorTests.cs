using KyrolusSous.Caching.Abstractions;
using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using NSubstitute;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusIdempotencyBehaviorTests
{
    public sealed record CreateOrderCommand(string OrderId, decimal Amount, string IdempotencyKey) : IIdempotentCommand<string>
    {
        public TimeSpan? IdempotencyTtl => TimeSpan.FromMinutes(30);
    }

    [Fact(DisplayName = "Idempotency: First execution executes handler and caches result")]
    public async Task Idempotency_FirstCall_ExecutesAndCachesResult()
    {
        var cache = Substitute.For<IKyrolusCacheProvider>();
        cache.GetAsync<string>(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>(null));

        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-123");

        var executionCount = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            executionCount++;
            return Task.FromResult("OrderCreated:ord-1");
        };

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.ShouldBe("OrderCreated:ord-1");
        executionCount.ShouldBe(1);
        await cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.Contains("key-123")),
            "OrderCreated:ord-1",
            Arg.Is<KyrolusCacheEntryOptions>(o => o.AbsoluteExpirationRelativeToNow == TimeSpan.FromMinutes(30)),
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Idempotency: Second execution with same key returns cached result without running handler")]
    public async Task Idempotency_SecondCall_ReturnsCachedWithoutHandler()
    {
        var cache = Substitute.For<IKyrolusCacheProvider>();
        cache.GetAsync<string>(Arg.Is<string>(k => k.Contains("key-123")), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<string?>("OrderCreated:ord-1"));

        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-123");

        var executionCount = 0;
        RequestHandlerDelegate<string> next = (ct) =>
        {
            executionCount++;
            return Task.FromResult("ShouldNotExecute");
        };

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.ShouldBe("OrderCreated:ord-1");
        executionCount.ShouldBe(0);
    }
}
