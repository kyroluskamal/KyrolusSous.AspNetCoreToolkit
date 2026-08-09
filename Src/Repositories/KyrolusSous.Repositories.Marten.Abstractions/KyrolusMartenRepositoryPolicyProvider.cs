namespace KyrolusSous.Repositories.Marten.Abstractions;

public sealed record KyrolusMartenRepositoryPolicyContext(
    Type EntityType,
    string EntityName,
    Type SessionType,
    string? ScopeKey,
    string? TenantId);

public interface IKyrolusMartenRepositoryPolicyProvider
{
    ValueTask<KyrolusMartenRepositoryDependencies?> GetPolicyAsync(
        KyrolusMartenRepositoryPolicyContext context,
        CancellationToken cancellationToken = default);
}

public sealed class KyrolusNoopMartenRepositoryPolicyProvider : IKyrolusMartenRepositoryPolicyProvider
{
    public static readonly IKyrolusMartenRepositoryPolicyProvider Instance = new KyrolusNoopMartenRepositoryPolicyProvider();

    public ValueTask<KyrolusMartenRepositoryDependencies?> GetPolicyAsync(
        KyrolusMartenRepositoryPolicyContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<KyrolusMartenRepositoryDependencies?>(null);
}
