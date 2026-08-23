using System.IO.Compression;
using KyrolusSous.Caching.Abstractions;
using KyrolusSous.Compression;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCacheOptions
{
    public string? ConnectionString { get; set; }
    public string? KeyPrefix { get; set; }
    public string KeyIndexKey { get; set; } = "kyrolus:cache:index";
    public int BatchSize { get; set; } = 256;
    public TimeSpan? DefaultTtl { get; set; } = KyrolusCacheDefaults.DefaultTtl;
    public TimeSpan? DefaultSlidingTtl { get; set; } = KyrolusCacheDefaults.DefaultSlidingTtl;
    public TimeSpan? DefaultNegativeTtl { get; set; }
    public TimeSpan? LockTtl { get; set; } = KyrolusCacheDefaults.DefaultLockTtl;
    public TimeSpan? LockWait { get; set; } = KyrolusCacheDefaults.DefaultLockWait;
    public TimeSpan? LockRetryDelay { get; set; } = KyrolusCacheDefaults.DefaultLockRetryDelay;
    public KyrolusRedisLockBackoffMode LockBackoffMode { get; set; } = KyrolusRedisLockBackoffMode.Fixed;
    public double LockBackoffMultiplier { get; set; } = 2;
    public TimeSpan? LockMaxRetryDelay { get; set; }
    public KyrolusRedisLockStrategy LockStrategy { get; set; } = KyrolusRedisLockStrategy.Lua;
    public KyrolusRedisPatternRemovalStrategy PatternRemovalStrategy { get; set; } = KyrolusRedisPatternRemovalStrategy.KeyIndex;
    public KyrolusRedisServerRole ScanServerRole { get; set; } = KyrolusRedisServerRole.Primary;
    public CommandFlags ReadCommandFlags { get; set; } = CommandFlags.None;
    public CommandFlags WriteCommandFlags { get; set; } = CommandFlags.None;
    public bool EnableCompression { get; set; }
    public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.Brotli;
    public int CompressionThresholdBytes { get; set; } = 1024;
    public CompressionLevel CompressionLevel { get; set; } = KyrolusCacheDefaults.DefaultCompressionLevel;
    public int CompressionOrder { get; set; }
    public bool EnableEncryption { get; set; }
    public byte[]? EncryptionKey { get; set; }
    public string? EncryptionKeyBase64 { get; set; }
    public byte[]? EncryptionIv { get; set; }
    public string? EncryptionIvBase64 { get; set; }
    public int EncryptionOrder { get; set; } = 100;
    public string ConfigSignatureKey { get; set; } = "kyrolus:cache:config";
    public Action<string>? WarningSink { get; set; }
    public string? DefaultRegion { get; set; }
    public string? DefaultTenantId { get; set; }
    public bool RequireRegion { get; set; }
    public bool RequireTenantId { get; set; }
    public bool EnableGracefulFallback { get; set; }
    public KyrolusRedisCircuitBreakerOptions CircuitBreaker { get; set; } = new();

    public KyrolusRedisCacheOptions UseConnectionString(string connectionString)
    {
        ConnectionString = connectionString;
        return this;
    }

    public KyrolusRedisCacheOptions WithKeyPrefix(string prefix)
    {
        KeyPrefix = prefix;
        return this;
    }

    public KyrolusRedisCacheOptions WithDefaultTtl(TimeSpan ttl)
    {
        DefaultTtl = ttl;
        return this;
    }

    public KyrolusRedisCacheOptions WithSlidingTtl(TimeSpan slidingTtl)
    {
        DefaultSlidingTtl = slidingTtl;
        return this;
    }

    public KyrolusRedisCacheOptions WithNegativeTtl(TimeSpan negativeTtl)
    {
        DefaultNegativeTtl = negativeTtl;
        return this;
    }

    public KyrolusRedisCacheOptions WithCompression(
        int thresholdBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest,
        CompressionAlgorithm algorithm = CompressionAlgorithm.Brotli)
    {
        EnableCompression = true;
        CompressionThresholdBytes = thresholdBytes;
        CompressionLevel = level;
        CompressionAlgorithm = algorithm;
        return this;
    }

    public KyrolusRedisCacheOptions WithBrotliCompression(
        int thresholdBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest) =>
        WithCompression(thresholdBytes, level, CompressionAlgorithm.Brotli);

    public KyrolusRedisCacheOptions WithZstdCompression(
        int thresholdBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest) =>
        WithCompression(thresholdBytes, level, CompressionAlgorithm.Zstd);

    public KyrolusRedisCacheOptions WithLz4Compression(
        int thresholdBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest) =>
        WithCompression(thresholdBytes, level, CompressionAlgorithm.Lz4);

    public KyrolusRedisCacheOptions WithSnappyCompression(
        int thresholdBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest) =>
        WithCompression(thresholdBytes, level, CompressionAlgorithm.Snappy);

    public KyrolusRedisCacheOptions WithGzipCompression(
        int thresholdBytes = 1024,
        CompressionLevel level = CompressionLevel.Fastest) =>
        WithCompression(thresholdBytes, level, CompressionAlgorithm.Gzip);

    public KyrolusRedisCacheOptions WithEncryption(byte[] key, byte[]? iv = null)
    {
        EnableEncryption = true;
        EncryptionKey = key;
        EncryptionIv = iv;
        return this;
    }

    public KyrolusRedisCacheOptions WithEncryptionBase64(string keyBase64, string? ivBase64 = null)
    {
        EnableEncryption = true;
        EncryptionKeyBase64 = keyBase64;
        EncryptionIvBase64 = ivBase64;
        return this;
    }

    public KyrolusRedisCacheOptions WithCircuitBreaker(Action<KyrolusRedisCircuitBreakerOptions>? configure = null)
    {
        CircuitBreaker.Enabled = true;
        configure?.Invoke(CircuitBreaker);
        return this;
    }

    public KyrolusRedisCacheOptions WithLockStrategy(
        KyrolusRedisLockStrategy strategy,
        TimeSpan? defaultWait = null,
        TimeSpan? defaultTtl = null)
    {
        LockStrategy = strategy;
        if (defaultWait.HasValue) LockWait = defaultWait.Value;
        if (defaultTtl.HasValue) LockTtl = defaultTtl.Value;
        return this;
    }

    public KyrolusRedisCacheOptions WithGracefulFallback(bool enable = true)
    {
        EnableGracefulFallback = enable;
        return this;
    }

    public KyrolusRedisCacheOptions WithNamespace(
        string? defaultRegion = null,
        string? defaultTenantId = null,
        bool requireRegion = false,
        bool requireTenantId = false)
    {
        DefaultRegion = defaultRegion;
        DefaultTenantId = defaultTenantId;
        RequireRegion = requireRegion;
        RequireTenantId = requireTenantId;
        return this;
    }
}
