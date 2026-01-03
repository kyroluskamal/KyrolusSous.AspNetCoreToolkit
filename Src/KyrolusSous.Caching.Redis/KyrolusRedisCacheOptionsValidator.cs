namespace KyrolusSous.Caching.Redis;

public static class KyrolusRedisCacheOptionsValidator
{
    public static void Validate(KyrolusRedisCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        EnsurePositive(options.BatchSize, nameof(options.BatchSize));
        EnsurePositive(options.DefaultTtl, nameof(options.DefaultTtl));
        EnsurePositive(options.DefaultSlidingTtl, nameof(options.DefaultSlidingTtl));
        EnsurePositive(options.DefaultNegativeTtl, nameof(options.DefaultNegativeTtl));
        EnsurePositive(options.LockTtl, nameof(options.LockTtl));
        EnsureNonNegative(options.LockWait, nameof(options.LockWait));
        EnsureNonNegative(options.LockRetryDelay, nameof(options.LockRetryDelay));

        if (options.LockBackoffMultiplier < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options.LockBackoffMultiplier), "LockBackoffMultiplier must be >= 1.");
        }

        EnsureNonNegative(options.LockMaxRetryDelay, nameof(options.LockMaxRetryDelay));

        if (options.EnableCompression && options.CompressionThresholdBytes <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options.CompressionThresholdBytes), "CompressionThresholdBytes must be > 0 when compression is enabled.");
        }

        if (options.EnableEncryption)
        {
            var key = ResolveBase64(options.EncryptionKey, options.EncryptionKeyBase64, nameof(options.EncryptionKeyBase64));
            if (key is null)
            {
                throw new ArgumentException("EncryptionKey or EncryptionKeyBase64 must be provided when encryption is enabled.");
            }

            if (key.Length is not (16 or 24 or 32))
            {
                throw new ArgumentException("EncryptionKey must be 16, 24, or 32 bytes when encryption is enabled.");
            }

            var iv = ResolveBase64(options.EncryptionIv, options.EncryptionIvBase64, nameof(options.EncryptionIvBase64));
            if (iv is { Length: > 0 } && iv.Length != 16)
            {
                throw new ArgumentException("EncryptionIv must be 16 bytes when provided.");
            }
        }

        if (options.CircuitBreaker.Enabled)
        {
            EnsurePositive(options.CircuitBreaker.FailureThreshold, nameof(options.CircuitBreaker.FailureThreshold));
            EnsurePositive(options.CircuitBreaker.OpenDuration, nameof(options.CircuitBreaker.OpenDuration));
            EnsurePositive(options.CircuitBreaker.MaxOpenDuration, nameof(options.CircuitBreaker.MaxOpenDuration));

            if (options.CircuitBreaker.BackoffMultiplier < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(options.CircuitBreaker.BackoffMultiplier), "BackoffMultiplier must be >= 1.");
            }

            EnsurePositive(options.CircuitBreaker.HalfOpenSuccesses, nameof(options.CircuitBreaker.HalfOpenSuccesses));
        }

        if (options.RequireRegion && string.IsNullOrWhiteSpace(options.DefaultRegion))
        {
            throw new InvalidOperationException("RequireRegion is enabled but DefaultRegion is empty.");
        }

        if (options.RequireTenantId && string.IsNullOrWhiteSpace(options.DefaultTenantId))
        {
            throw new InvalidOperationException("RequireTenantId is enabled but DefaultTenantId is empty.");
        }
    }

    public static void Validate(KyrolusRedisNearCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.InvalidationChannel))
        {
            throw new ArgumentException("InvalidationChannel cannot be empty.");
        }

        EnsurePositive(options.DefaultL1Ttl, nameof(options.DefaultL1Ttl));
        EnsurePositive(options.DefaultL1SlidingTtl, nameof(options.DefaultL1SlidingTtl));
        EnsureNonNegative(options.L1Jitter, nameof(options.L1Jitter));
    }

    public static void Validate(KyrolusRedisInvalidationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Channel))
        {
            throw new ArgumentException("Channel cannot be empty.");
        }
    }

    private static void EnsurePositive(TimeSpan? value, string name)
    {
        if (value is null)
        {
            return;
        }

        if (value.Value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, $"{name} must be > 0.");
        }
    }

    private static void EnsurePositive(int value, string name)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(name, $"{name} must be > 0.");
        }
    }

    private static void EnsureNonNegative(TimeSpan? value, string name)
    {
        if (value is null)
        {
            return;
        }

        if (value.Value < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(name, $"{name} must be >= 0.");
        }
    }

    private static byte[]? ResolveBase64(byte[]? raw, string? base64, string base64Name)
    {
        if (raw is { Length: > 0 })
        {
            return raw;
        }

        if (string.IsNullOrWhiteSpace(base64))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException ex)
        {
            throw new ArgumentException($"{base64Name} is not valid Base64.", ex);
        }
    }
}
