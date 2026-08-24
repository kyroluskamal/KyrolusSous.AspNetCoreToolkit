namespace KyrolusSous.Caching.Abstractions;

/// <summary>
/// Thread-safe registry providing hierarchical policy lookup for repository operations.
/// Supports granular configurations per Tenant, Entity Type, Operation, or Global Defaults.
/// </summary>
/// <remarks>
/// <b>Resolution Precedence Order:</b>
/// <list type="number">
///   <item><description>Tenant + Entity Type + Operation (<c>SetForTenantType&lt;T&gt;</c>)</description></item>
///   <item><description>Tenant + Operation (<c>SetForTenantOperation</c>)</description></item>
///   <item><description>Tenant Global (<c>SetForTenant</c>)</description></item>
///   <item><description>Entity Type + Operation (<c>SetForType&lt;T&gt;</c>)</description></item>
///   <item><description>Operation Global (<c>SetForOperation</c>)</description></item>
///   <item><description>Fallback Default (<c>SetDefault</c>)</description></item>
/// </list>
/// </remarks>
public sealed class KyrolusRepositoryCachePolicyRegistry : IKyrolusRepositoryCachePolicyProvider
{
    private readonly ConcurrentDictionary<(string TenantId, Type, string), KyrolusCachePolicy> byTenantTypeAndOperation = new();
    private readonly ConcurrentDictionary<(string TenantId, string), KyrolusCachePolicy> byTenantOperation = new();
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byTenant = new();
    private readonly ConcurrentDictionary<(Type, string), KyrolusCachePolicy> byTypeAndOperation = new();
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byOperation = new();
    private KyrolusCachePolicy? defaultPolicy;

    /// <summary>
    /// Sets the fallback default cache policy applied to all repository operations.
    /// </summary>
    /// <param name="policy">The fallback cache policy.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusRepositoryCachePolicyRegistry SetDefault(KyrolusCachePolicy policy)
    {
        defaultPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a cache policy applicable to any entity executing the specified operation (e.g. "GetById").
    /// </summary>
    /// <param name="operation">The repository operation name.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusRepositoryCachePolicyRegistry SetForOperation(string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byOperation[operation] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a tenant-wide cache policy applicable to all operations for the specified tenant.
    /// </summary>
    /// <param name="tenantId">The unique tenant ID.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusRepositoryCachePolicyRegistry SetForTenant(string tenantId, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        byTenant[tenantId] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a cache policy for a specific tenant and operation.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="operation">The repository operation name.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusRepositoryCachePolicyRegistry SetForTenantOperation(string tenantId, string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byTenantOperation[(tenantId, operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a cache policy for a specific tenant, entity type <typeparamref name="T"/>, and operation.
    /// </summary>
    /// <typeparam name="T">The database entity type.</typeparam>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="operation">The repository operation name.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusRepositoryCachePolicyRegistry SetForTenantType<T>(string tenantId, string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byTenantTypeAndOperation[(tenantId, typeof(T), operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <summary>
    /// Sets a cache policy for an entity type <typeparamref name="T"/> and operation across all tenants.
    /// </summary>
    /// <typeparam name="T">The database entity type.</typeparam>
    /// <param name="operation">The repository operation name.</param>
    /// <param name="policy">The policy to apply.</param>
    /// <returns>The current registry instance for fluent chaining.</returns>
    public KyrolusRepositoryCachePolicyRegistry SetForType<T>(string operation, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(operation))
            throw new ArgumentException("Operation is required.", nameof(operation));
        byTypeAndOperation[(typeof(T), operation)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    /// <inheritdoc />
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
