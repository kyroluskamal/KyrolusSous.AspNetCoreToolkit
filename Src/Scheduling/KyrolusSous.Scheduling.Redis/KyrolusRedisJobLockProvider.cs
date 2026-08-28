using KyrolusSous.Scheduling.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KyrolusSous.Scheduling.Redis;

public sealed class KyrolusRedisJobLockProvider(IConnectionMultiplexer redis) : IKyrolusJobLockProvider
{
    private readonly IDatabase _db = (redis ?? throw new ArgumentNullException(nameof(redis))).GetDatabase();

    private sealed class RedisLockReleaser(IDatabase db, string lockKey, string lockValue) : IAsyncDisposable
    {
        private const string UnlockScript = @"
            if redis.call('get', KEYS[1]) == ARGV[1] then
                return redis.call('del', KEYS[1])
            else
                return 0
            end";

        private int _disposed;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                await db.ScriptEvaluateAsync(UnlockScript, [new RedisKey(lockKey)], [new RedisValue(lockValue)]).ConfigureAwait(false);
            }
        }
    }

    public async ValueTask<IAsyncDisposable?> TryAcquireLockAsync(string lockKey, TimeSpan lockDuration, CancellationToken cancellationToken = default)
    {
        var fullKey = $"kyrolus:sched:lock:{lockKey}";
        var lockValue = Guid.NewGuid().ToString("N");

        var acquired = await _db.StringSetAsync(fullKey, lockValue, lockDuration, When.NotExists).ConfigureAwait(false);
        if (acquired)
        {
            return new RedisLockReleaser(_db, fullKey, lockValue);
        }

        return null;
    }
}

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddKyrolusRedisJobLock(this IServiceCollection services)
    {
        services.AddSingleton<IKyrolusJobLockProvider, KyrolusRedisJobLockProvider>();
        return services;
    }
}
