using System.IO.Compression;
using KyrolusSous.Caching.Abstractions;
using StackExchange.Redis;

namespace KyrolusSous.Caching.Redis;

public sealed class KyrolusRedisCacheOptions
{
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
    public int CompressionThresholdBytes { get; set; } = KyrolusCacheDefaults.DefaultCompressionThresholdBytes;
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
}
