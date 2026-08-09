namespace KyrolusSous.Caching.Abstractions;

public interface IKyrolusRepositoryCachePolicyProvider
{
    ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusRepositoryCachePolicyContext context,
        CancellationToken cancellationToken = default);
}

public sealed class KyrolusNoopRepositoryCachePolicyProvider : IKyrolusRepositoryCachePolicyProvider
{
    public static readonly IKyrolusRepositoryCachePolicyProvider Instance = new KyrolusNoopRepositoryCachePolicyProvider();

    public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusRepositoryCachePolicyContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<KyrolusCachePolicy?>(null);
}
