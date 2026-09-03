using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.CQRS.Caching;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Shouldly;
using Xunit;

namespace KyrolusSous.CQRS.UnitTests;

public sealed class KyrolusIdempotencyBehaviorTests
{
    public sealed record CreateOrderCommand(string OrderId, decimal Amount, string IdempotencyKey) : IKyrolusIdempotentCommand<string>
    {
        public TimeSpan? IdempotencyTtl => TimeSpan.FromMinutes(30);
    }

    public sealed record VoidIdempotentCommand(string IdempotencyKey) : IKyrolusCommand, IKyrolusIdempotentCommand;

    [Fact(DisplayName = "Idempotency: First execution executes handler and caches result")]
    public async Task Idempotency_FirstCall_ExecutesAndCachesResult()
    {
        var cache = new FakeCacheProvider();
        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-123");

        var executionCount = 0;
        RequestHandlerDelegate<string> next = _ =>
        {
            executionCount++;
            return Task.FromResult("OrderCreated:ord-1");
        };

        var result = await behavior.Handle(command, next, CancellationToken.None);

        result.ShouldBe("OrderCreated:ord-1");
        executionCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Idempotency: Second execution with same key returns cached result without running handler")]
    public async Task Idempotency_SecondCall_ReturnsCachedWithoutHandler()
    {
        var cache = new FakeCacheProvider();
        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-123");

        var executionCount = 0;
        RequestHandlerDelegate<string> next = _ =>
        {
            executionCount++;
            return Task.FromResult("OrderCreated:ord-1");
        };

        var first = await behavior.Handle(command, next, CancellationToken.None);
        var second = await behavior.Handle(command, next, CancellationToken.None);

        first.ShouldBe("OrderCreated:ord-1");
        second.ShouldBe("OrderCreated:ord-1");
        executionCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Idempotency: A key already claimed by an in-flight execution is rejected, not re-run")]
    public async Task Idempotency_ConcurrentClaim_ThrowsInsteadOfDoubleExecuting()
    {
        var cache = new FakeCacheProvider();
        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-123");

        // Simulate another in-flight caller: the key is claimed (SetIfNotExistsAsync already
        // succeeded for them) but they have not finished, so no completed record exists yet.
        cache.Seed("idempotency:CreateOrderCommand:key-123", new KyrolusIdempotencyRecord<string> { Completed = false });

        var executionCount = 0;
        RequestHandlerDelegate<string> next = _ =>
        {
            executionCount++;
            return Task.FromResult("ShouldNotExecute");
        };

        await Should.ThrowAsync<KyrolusIdempotencyConflictException>(
            () => behavior.Handle(command, next, CancellationToken.None));

        executionCount.ShouldBe(0);
    }

    [Fact(DisplayName = "Idempotency: A failed first attempt releases the claim so a retry can proceed")]
    public async Task Idempotency_FailedFirstAttempt_ReleasesClaimForRetry()
    {
        var cache = new FakeCacheProvider();
        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-123");

        RequestHandlerDelegate<string> failing = _ => throw new InvalidOperationException("boom");
        await Should.ThrowAsync<InvalidOperationException>(() => behavior.Handle(command, failing, CancellationToken.None));

        var executionCount = 0;
        RequestHandlerDelegate<string> succeeding = _ =>
        {
            executionCount++;
            return Task.FromResult("OrderCreated:ord-1");
        };
        var result = await behavior.Handle(command, succeeding, CancellationToken.None);

        result.ShouldBe("OrderCreated:ord-1");
        executionCount.ShouldBe(1);
    }

    [Fact(DisplayName = "Idempotency: works for commands with no response value (Unit), not just typed responses")]
    public async Task Idempotency_UnitResponse_StillDedupes()
    {
        // Regression test: Unit's equality always returns true (all instances are "equal to
        // default"), so a naive "was something cached?" check via EqualityComparer<Unit>.Equals
        // could never tell "nothing cached yet" apart from "Unit.Value was cached" - the envelope
        // (KyrolusIdempotencyRecord.Completed) is what makes this distinguishable.
        var cache = new FakeCacheProvider();
        var behavior = new KyrolusIdempotencyBehavior<VoidIdempotentCommand, Unit>(cache);
        var command = new VoidIdempotentCommand("key-456");

        var executionCount = 0;
        RequestHandlerDelegate<Unit> next = _ =>
        {
            executionCount++;
            return Task.FromResult(Unit.Value);
        };

        await behavior.Handle(command, next, CancellationToken.None);
        await behavior.Handle(command, next, CancellationToken.None);

        executionCount.ShouldBe(1);
    }
}
