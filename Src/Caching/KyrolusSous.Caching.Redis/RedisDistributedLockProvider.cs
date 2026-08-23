using System.Diagnostics;
using KyrolusSous.Caching.Abstractions;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

/// <summary>
/// Implements <see cref="IDistributedLockProvider"/> using Redis with atomic Lua scripts.
/// </summary>
public sealed class RedisDistributedLockProvider : IDistributedLockProvider
{
    private const string AcquireLockScript =
        "if redis.call('exists', KEYS[1]) == 0 then redis.call('psetex', KEYS[1], ARGV[2], ARGV[1]); return 1 else return 0 end";
    private const string ReleaseLockScript =
        "if redis.call('get', KEYS[1]) == ARGV[1] then return redis.call('del', KEYS[1]) else return 0 end";
    private const string LockSuffix = ":lock";

    private readonly IConnectionMultiplexer multiplexer;
    private readonly IDatabase database;
    private readonly IKyrolusCacheKeyFactory keyFactory;
    private readonly KyrolusRedisCacheOptions options;

    public RedisDistributedLockProvider(
        IConnectionMultiplexer multiplexer,
        IKyrolusCacheKeyFactory? keyFactory = null,
        KyrolusRedisCacheOptions? options = null)
    {
        this.multiplexer = multiplexer ?? throw new ArgumentNullException(nameof(multiplexer));
        this.database = multiplexer.GetDatabase();
        this.options = options ?? new KyrolusRedisCacheOptions();
        this.keyFactory = keyFactory ?? new KyrolusCacheKeyFactory(this.options.KeyPrefix);
    }

    public async Task<IDistributedLockHandle?> TryAcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout), "Timeout must be non-negative.");

        var resolvedKey = keyFactory.BuildKey($"{key}{LockSuffix}");
        var token = Guid.NewGuid().ToString("N");
        var expiry = lockExpiry ?? options.LockTtl ?? KyrolusCacheDefaults.DefaultLockTtl;
        var expiryMs = (long)Math.Max(1, expiry.TotalMilliseconds);

        var waitUntil = DateTimeOffset.UtcNow + timeout;
        var attempt = 0;

        while (DateTimeOffset.UtcNow <= waitUntil)
        {
            cancellationToken.ThrowIfCancellationRequested();
            attempt++;

            var result = await database.ScriptEvaluateAsync(
                AcquireLockScript,
                [resolvedKey],
                [(RedisValue)token, expiryMs],
                options.WriteCommandFlags).ConfigureAwait(false);

            if ((int)result == 1)
            {
                return new RedisDistributedLockHandle(database, resolvedKey, token, key, options.WriteCommandFlags);
            }

            var delay = GetRetryDelay(attempt, waitUntil);
            if (delay > TimeSpan.Zero)
            {
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                break;
            }
        }

        return null;
    }

    public async Task<IDistributedLockHandle> AcquireLockAsync(
        string key,
        TimeSpan timeout,
        TimeSpan? lockExpiry = null,
        CancellationToken cancellationToken = default)
    {
        var handle = await TryAcquireLockAsync(key, timeout, lockExpiry, cancellationToken).ConfigureAwait(false);
        if (handle is null)
        {
            throw new TimeoutException($"Failed to acquire distributed lock for key '{key}' within timeout of {timeout}.");
        }
        return handle;
    }

    private TimeSpan GetRetryDelay(int attempt, DateTimeOffset waitUntil)
    {
        var remaining = waitUntil - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return TimeSpan.Zero;

        var baseDelay = options.LockRetryDelay ?? KyrolusCacheDefaults.DefaultLockRetryDelay;
        if (baseDelay <= TimeSpan.Zero) baseDelay = TimeSpan.FromMilliseconds(50);

        TimeSpan delay;
        if (options.LockBackoffMode == KyrolusRedisLockBackoffMode.Exponential)
        {
            var multiplier = Math.Max(1, options.LockBackoffMultiplier);
            var factor = Math.Pow(multiplier, Math.Max(0, attempt - 1));
            delay = TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * factor);
            if (options.LockMaxRetryDelay is { } max && max > TimeSpan.Zero && delay > max)
                delay = max;
        }
        else
        {
            delay = baseDelay;
        }

        return delay < remaining ? delay : remaining;
    }

    private sealed class RedisDistributedLockHandle : IDistributedLockHandle
    {
        private readonly IDatabase database;
        private readonly RedisKey resolvedKey;
        private readonly CommandFlags writeFlags;
        private int released;

        public string LockKey { get; }
        public string LockToken { get; }
        public bool IsAcquired => released == 0;

        public RedisDistributedLockHandle(
            IDatabase database,
            RedisKey resolvedKey,
            string token,
            string originalKey,
            CommandFlags writeFlags)
        {
            this.database = database;
            this.resolvedKey = resolvedKey;
            this.LockToken = token;
            this.LockKey = originalKey;
            this.writeFlags = writeFlags;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                await database.ScriptEvaluateAsync(
                    ReleaseLockScript,
                    [resolvedKey],
                    [(RedisValue)LockToken],
                    writeFlags).ConfigureAwait(false);
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref released, 1) == 0)
            {
                database.ScriptEvaluate(
                    ReleaseLockScript,
                    [resolvedKey],
                    [(RedisValue)LockToken],
                    writeFlags);
            }
        }
    }
}
