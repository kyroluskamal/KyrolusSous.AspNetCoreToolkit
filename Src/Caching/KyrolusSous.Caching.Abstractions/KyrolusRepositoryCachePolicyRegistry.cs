using System.Collections.Concurrent;

namespace KyrolusSous.Caching.Abstractions;

public sealed class KyrolusRepositoryCachePolicyRegistry : IKyrolusRepositoryCachePolicyProvider
{
    private readonly ConcurrentDictionary<(string TenantId, Type, string), KyrolusCachePolicy> byTenantTypeAndOperation = new();
    private readonly ConcurrentDictionary<(string TenantId, string), KyrolusCachePolicy> byTenantOperation = new();
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byTenant = new();
    private readonly ConcurrentDictionary<(Type, string), KyrolusCachePolicy> byTypeAndOperation = new();
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byOperation = new();
    private KyrolusCachePolicy? defaultPolicy;

    public KyrolusRepositoryCachePolicyRegistry SetDefault(KyrolusCachePolicy policy)
    {
        defaultPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusRepositoryCachePolicyRegistry SetForOperation(string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byOperation[operation] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusRepositoryCachePolicyRegistry SetForTenant(string tenantId, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        byTenant[tenantId] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusRepositoryCachePolicyRegistry SetForTenantOperation(string tenantId, string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byTenantOperation[(tenantId, operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusRepositoryCachePolicyRegistry SetForTenantType<T>(string tenantId, string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byTenantTypeAndOperation[(tenantId, typeof(T), operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusRepositoryCachePolicyRegistry SetForType<T>(string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byTypeAndOperation[(typeof(T), operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusRepositoryCachePolicyContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!string.IsNullOrWhiteSpace(context.TenantId))
        {
            var tenantId = context.TenantId!;
            if (!string.IsNullOrWhiteSpace(context.Operation)
                && byTenantTypeAndOperation.TryGetValue((tenantId, context.EntityType, context.Operation), out var tenantPolicy))
            {
                return ValueTask.FromResult<KyrolusCachePolicy?>(tenantPolicy);
            }

            if (!string.IsNullOrWhiteSpace(context.Operation)
                && byTenantOperation.TryGetValue((tenantId, context.Operation), out tenantPolicy))
            {
                return ValueTask.FromResult<KyrolusCachePolicy?>(tenantPolicy);
            }

            if (byTenant.TryGetValue(tenantId, out tenantPolicy))
            {
                return ValueTask.FromResult<KyrolusCachePolicy?>(tenantPolicy);
            }
        }

        if (!string.IsNullOrWhiteSpace(context.Operation)
            && byTypeAndOperation.TryGetValue((context.EntityType, context.Operation), out var policy))
        {
            return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
        }

        if (!string.IsNullOrWhiteSpace(context.Operation)
            && byOperation.TryGetValue(context.Operation, out policy))
        {
            return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
        }

        return ValueTask.FromResult(defaultPolicy);
    }
}
