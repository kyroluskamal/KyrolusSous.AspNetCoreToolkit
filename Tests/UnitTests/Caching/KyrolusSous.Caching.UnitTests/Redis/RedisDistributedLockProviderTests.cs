namespace KyrolusSous.Caching.UnitTests.Redis;

public sealed class RedisDistributedLockProviderTests
{
    [Fact(DisplayName = "RedisDistributedLockProvider: Successfully acquires lock and releases via Lua script upon DisposeAsync")]
    public async Task AcquireLock_And_DisposeAsync_Releases()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        // Simulate Lua script returning 1 (Lock Acquired)
        db.ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Any<RedisKey[]>(),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(RedisResult.Create((RedisValue)1)));

        var provider = new RedisDistributedLockProvider(muxer);

        await using (var handle = await provider.AcquireLockAsync("wallet:101", TimeSpan.FromSeconds(2)))
        {
            handle.ShouldNotBeNull();
            handle.IsAcquired.ShouldBeTrue();
            handle.LockKey.ShouldBe("wallet:101");
            handle.LockToken.ShouldNotBeNullOrWhiteSpace();
        }

        // ScriptEvaluateAsync should be called twice: 1 for Acquire, 1 for Release
        await db.Received(2).ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Any<RedisKey[]>(),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>());
    }

    [Fact(DisplayName = "RedisDistributedLockProvider: Failed acquisition throws TimeoutException in AcquireLockAsync")]
    public async Task AcquireLock_Timeout_ThrowsTimeoutException()
    {
        var muxer = Substitute.For<IConnectionMultiplexer>();
        var db = Substitute.For<IDatabase>();
        muxer.GetDatabase(Arg.Any<int>(), Arg.Any<object>()).Returns(db);

        // Simulate Lua script returning 0 (Lock Busy / Failed)
        db.ScriptEvaluateAsync(
            Arg.Any<string>(),
            Arg.Any<RedisKey[]>(),
            Arg.Any<RedisValue[]>(),
            Arg.Any<CommandFlags>()).Returns(Task.FromResult(RedisResult.Create((RedisValue)0)));

        var provider = new RedisDistributedLockProvider(muxer);

        await Should.ThrowAsync<TimeoutException>(async () =>
        {
            await provider.AcquireLockAsync("busy:resource", TimeSpan.FromMilliseconds(50));
        });
    }
}
