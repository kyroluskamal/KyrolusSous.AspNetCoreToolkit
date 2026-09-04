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
        cache.Seed($"idempotency:{typeof(CreateOrderCommand).FullName}:key-123", new KyrolusIdempotencyRecord<string> { Completed = false });

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

    [Fact(DisplayName = "Idempotency: cancellation observed after the handler already ran does not release the claim - a retry with the same key is rejected, not re-run")]
    public async Task Idempotency_CancelledAfterHandlerRan_RetryIsRejectedNotReRun()
    {
        var cache = new FakeCacheProvider();
        using var cts = new CancellationTokenSource();
        var behavior = new KyrolusIdempotencyBehavior<CreateOrderCommand, string>(cache);
        var command = new CreateOrderCommand("ord-1", 100m, "key-cancel");

        RequestHandlerDelegate<string> next = _ =>
        {
            // The handler already did its work (e.g. money moved) by the time cancellation is
            // observed - this must not be treated like an ordinary failure.
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        await Should.ThrowAsync<OperationCanceledException>(() => behavior.Handle(command, next, cts.Token));

        var retryCount = 0;
        RequestHandlerDelegate<string> retry = _ =>
        {
            retryCount++;
            return Task.FromResult("ShouldNotExecute");
        };

        await Should.ThrowAsync<KyrolusIdempotencyConflictException>(
            () => behavior.Handle(command, retry, CancellationToken.None));

        retryCount.ShouldBe(0);
    }

    private static class CollisionA
    {
        public sealed record CollidingCommand(string IdempotencyKey) : IKyrolusIdempotentCommand<string>
        {
            public TimeSpan? IdempotencyTtl => null;
        }
    }

    private static class CollisionB
    {
        public sealed record CollidingCommand(string IdempotencyKey) : IKyrolusIdempotentCommand<string>
        {
            public TimeSpan? IdempotencyTtl => null;
        }
    }

    [Fact(DisplayName = "Idempotency: cache key is namespaced by FullName, so two unrelated command types sharing a short class name and the same key value do not share a reservation")]
    public async Task Idempotency_SameShortNameDifferentNamespace_DoesNotShareReservation()
    {
        var cache = new FakeCacheProvider();
        var behaviorA = new KyrolusIdempotencyBehavior<CollisionA.CollidingCommand, string>(cache);
        var behaviorB = new KyrolusIdempotencyBehavior<CollisionB.CollidingCommand, string>(cache);

        await behaviorA.Handle(new CollisionA.CollidingCommand("shared-key"), _ => Task.FromResult("A-result"), CancellationToken.None);

        var bExecutionCount = 0;
        var resultB = await behaviorB.Handle(
            new CollisionB.CollidingCommand("shared-key"),
            _ =>
            {
                bExecutionCount++;
                return Task.FromResult("B-result");
            },
            CancellationToken.None);

        // Before the FullName fix, both mapped to "idempotency:CollidingCommand:shared-key" - B would
        // read A's completed record and return "A-result" without running its own handler at all.
        bExecutionCount.ShouldBe(1);
        resultB.ShouldBe("B-result");
    }
}
