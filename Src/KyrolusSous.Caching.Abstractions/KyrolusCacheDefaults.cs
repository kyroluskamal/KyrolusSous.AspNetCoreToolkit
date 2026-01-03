using System.IO.Compression;

namespace KyrolusSous.Caching.Abstractions;

public static class KyrolusCacheDefaults
{
    public static TimeSpan DefaultTtl { get; } = TimeSpan.FromMinutes(30);
    public static TimeSpan DefaultSlidingTtl { get; } = TimeSpan.FromMinutes(5);
    public static TimeSpan DefaultLockTtl { get; } = TimeSpan.FromSeconds(10);
    public static TimeSpan DefaultLockWait { get; } = TimeSpan.FromSeconds(2);
    public static TimeSpan DefaultLockRetryDelay { get; } = TimeSpan.FromMilliseconds(50);
    public static int DefaultCompressionThresholdBytes { get; } = 1024;
    public static CompressionLevel DefaultCompressionLevel { get; } = CompressionLevel.Fastest;
}
