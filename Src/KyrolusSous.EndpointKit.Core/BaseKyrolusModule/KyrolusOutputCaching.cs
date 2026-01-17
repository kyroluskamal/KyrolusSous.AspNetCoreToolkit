using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed record KyrolusOutputCacheEntry(
    object? Value,
    int StatusCode,
    string? ContentType);

public sealed record KyrolusEndpointCachePolicyContext(
    Type EntityType,
    string EntityName,
    EndpointNames Endpoint,
    string HttpMethod,
    string Path,
    string? TenantId,
    string? ScopeKey);

public interface IKyrolusEndpointCachePolicyProvider
{
    ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusEndpointCachePolicyContext context,
        CancellationToken cancellationToken = default);
}

public sealed class KyrolusNoopEndpointCachePolicyProvider : IKyrolusEndpointCachePolicyProvider
{
    public static readonly IKyrolusEndpointCachePolicyProvider Instance = new KyrolusNoopEndpointCachePolicyProvider();

    public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusEndpointCachePolicyContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<KyrolusCachePolicy?>(null);
}

[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
public sealed class KyrolusOutputCacheAttribute : Attribute
{
    public bool Enabled { get; set; } = true;
    public int? AbsoluteExpirationSeconds { get; set; }
    public int? SlidingExpirationSeconds { get; set; }
    public int? JitterSeconds { get; set; }
    public int? NegativeCacheTtlSeconds { get; set; }
    public string? KeySuffix { get; set; }

    internal KyrolusCachePolicy ToPolicy()
    {
        return new KyrolusCachePolicy(
            AbsoluteExpirationRelativeToNow: AbsoluteExpirationSeconds is > 0 ? TimeSpan.FromSeconds(AbsoluteExpirationSeconds.Value) : null,
            SlidingExpiration: SlidingExpirationSeconds is > 0 ? TimeSpan.FromSeconds(SlidingExpirationSeconds.Value) : null,
            Jitter: JitterSeconds is > 0 ? TimeSpan.FromSeconds(JitterSeconds.Value) : null,
            NegativeCacheTtl: NegativeCacheTtlSeconds is > 0 ? TimeSpan.FromSeconds(NegativeCacheTtlSeconds.Value) : null,
            Enabled: Enabled,
            KeySuffix: KeySuffix);
    }
}
