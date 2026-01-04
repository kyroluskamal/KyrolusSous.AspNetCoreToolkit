using System.IO.Compression;
using BenchmarkDotNet.Attributes;
using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.Caching.Benchmarks;

[MemoryDiagnoser]
public class CacheSerializerBenchmarks
{
    private KyrolusJsonCacheSerializer json = null!;
    private KyrolusTransformingCacheSerializer gzipSerializer = null!;
    private KyrolusTransformingCacheSerializer gzipAesSerializer = null!;
    private KyrolusCacheKeyFactory keyFactory = null!;
    private SamplePayload payload = null!;
    private byte[] jsonBytes = [];
    private byte[] gzipBytes = [];
    private byte[] gzipAesBytes = [];

    [Params(128, 1024)]
    public int PayloadSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        json = new KyrolusJsonCacheSerializer();
        keyFactory = new KyrolusCacheKeyFactory("bench");

        var gzip = new KyrolusGzipCachePayloadTransformer(512, CompressionLevel.Fastest);
        var key = new byte[32];
        var iv = new byte[16];
        for (var index = 0; index < key.Length; index++)
        {
            key[index] = (byte)(index + 1);
        }
        for (var index = 0; index < iv.Length; index++)
        {
            iv[index] = (byte)(index + 10);
        }

        var aes = new KyrolusAesCachePayloadTransformer(key, iv);

        gzipSerializer = new KyrolusTransformingCacheSerializer(json, new IKyrolusCachePayloadTransformer[] { gzip });
        gzipAesSerializer = new KyrolusTransformingCacheSerializer(json, new IKyrolusCachePayloadTransformer[] { gzip, aes });

        payload = SamplePayload.Create(PayloadSize);
        jsonBytes = json.Serialize(payload);
        gzipBytes = gzipSerializer.Serialize(payload);
        gzipAesBytes = gzipAesSerializer.Serialize(payload);
    }

    [Benchmark]
    public byte[] Serialize_Json() => json.Serialize(payload);

    [Benchmark]
    public SamplePayload? Deserialize_Json() => json.Deserialize<SamplePayload>(jsonBytes);

    [Benchmark]
    public byte[] Serialize_Gzip() => gzipSerializer.Serialize(payload);

    [Benchmark]
    public SamplePayload? Deserialize_Gzip() => gzipSerializer.Deserialize<SamplePayload>(gzipBytes);

    [Benchmark]
    public byte[] Serialize_GzipAes() => gzipAesSerializer.Serialize(payload);

    [Benchmark]
    public SamplePayload? Deserialize_GzipAes() => gzipAesSerializer.Deserialize<SamplePayload>(gzipAesBytes);

    [Benchmark]
    public string BuildKey_Default() => keyFactory.BuildKey("products:42");

    [Benchmark]
    public string BuildKey_WithRegionTenant() => keyFactory.BuildKey("products:42", "catalog", "tenant-1");

    public sealed class SamplePayload
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public int Version { get; init; }
        public string[] Tags { get; init; } = [];
        public Dictionary<string, string> Attributes { get; init; } = new(StringComparer.Ordinal);
        public int[] Numbers { get; init; } = [];

        public static SamplePayload Create(int size)
        {
            var text = new string('x', size);
            return new SamplePayload
            {
                Id = Guid.NewGuid(),
                Name = "Sample",
                Description = text,
                Version = 1,
                Tags = new[] { "a", "b", "c", "d" },
                Attributes = new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["createdBy"] = "bench",
                    ["region"] = "catalog",
                    ["tenant"] = "tenant-1"
                },
                Numbers = Enumerable.Range(1, 32).ToArray()
            };
        }
    }
}
