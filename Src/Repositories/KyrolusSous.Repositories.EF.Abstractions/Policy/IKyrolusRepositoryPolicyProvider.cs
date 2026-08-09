namespace KyrolusSous.Repositories.EF.Abstractions.Policy;

public sealed record KyrolusRepositoryPolicyContext(
    Type EntityType,
    string EntityName,
    Type DbContextType,
    string? ScopeKey,
    string? TenantId);

public interface IKyrolusRepositoryPolicyProvider
{
    ValueTask<KyrolusRepositoryPolicy?> GetPolicyAsync(
        KyrolusRepositoryPolicyContext context,
        CancellationToken cancellationToken = default);
}

public sealed class KyrolusNoopRepositoryPolicyProvider : IKyrolusRepositoryPolicyProvider
{
    public static readonly IKyrolusRepositoryPolicyProvider Instance = new KyrolusNoopRepositoryPolicyProvider();

    public ValueTask<KyrolusRepositoryPolicy?> GetPolicyAsync(
        KyrolusRepositoryPolicyContext context,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult<KyrolusRepositoryPolicy?>(null);
}
