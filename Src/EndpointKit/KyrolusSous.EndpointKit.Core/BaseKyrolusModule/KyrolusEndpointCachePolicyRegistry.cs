using System.Collections.Concurrent;
using KyrolusSous.Caching.Abstractions;

namespace KyrolusSous.EndpointKit.Core.BaseKyrolusModule;

public sealed class KyrolusEndpointCachePolicyRegistry : IKyrolusEndpointCachePolicyProvider
{
    private readonly ConcurrentDictionary<(string TenantId, Type EntityType, EndpointNames Endpoint), KyrolusCachePolicy> byTenantEntityEndpoint = new();
    private readonly ConcurrentDictionary<(string TenantId, EndpointNames Endpoint), KyrolusCachePolicy> byTenantEndpoint = new();
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byTenantRoute = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byTenant = new(StringComparer.Ordinal);

    private readonly ConcurrentDictionary<(Type EntityType, EndpointNames Endpoint), KyrolusCachePolicy> byEntityEndpoint = new();
    private readonly ConcurrentDictionary<EndpointNames, KyrolusCachePolicy> byEndpoint = new();
    private readonly ConcurrentDictionary<string, KyrolusCachePolicy> byRoute = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Type, KyrolusCachePolicy> byEntity = new();
    private KyrolusCachePolicy? defaultPolicy;

    public KyrolusEndpointCachePolicyRegistry SetDefault(KyrolusCachePolicy policy)
    {
        defaultPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForEndpoint(EndpointNames endpoint, KyrolusCachePolicy policy)
    {
        byEndpoint[endpoint] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForEntity<TEntity>(KyrolusCachePolicy policy)
    {
        byEntity[typeof(TEntity)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForEntity<TEntity>(EndpointNames endpoint, KyrolusCachePolicy policy)
    {
        byEntityEndpoint[(typeof(TEntity), endpoint)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForRoute(string httpMethod, string path, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(httpMethod))
            throw new ArgumentException("HTTP method is required.", nameof(httpMethod));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        byRoute[BuildRouteKey(httpMethod, path)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForTenant(string tenantId, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        byTenant[tenantId] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForTenantEndpoint(string tenantId, EndpointNames endpoint, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        byTenantEndpoint[(tenantId, endpoint)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForTenantEntity<TEntity>(string tenantId, EndpointNames endpoint, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        byTenantEntityEndpoint[(tenantId, typeof(TEntity), endpoint)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public KyrolusEndpointCachePolicyRegistry SetForTenantRoute(string tenantId, string httpMethod, string path, KyrolusCachePolicy policy)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("Tenant id is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(httpMethod))
            throw new ArgumentException("HTTP method is required.", nameof(httpMethod));
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));
        byTenantRoute[BuildTenantRouteKey(tenantId, httpMethod, path)] = policy ?? throw new ArgumentNullException(nameof(policy));
        return this;
    }

    public ValueTask<KyrolusCachePolicy?> GetPolicyAsync(
        KyrolusEndpointCachePolicyContext context,
        CancellationToken cancellationToken = default)
    {
        if (context is null) throw new ArgumentNullException(nameof(context));

        if (!string.IsNullOrWhiteSpace(context.TenantId))
        {
            var tenantId = context.TenantId!;
            if (byTenantEntityEndpoint.TryGetValue((tenantId, context.EntityType, context.Endpoint), out var policy))
                return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
            if (byTenantEndpoint.TryGetValue((tenantId, context.Endpoint), out policy))
                return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
            if (byTenantRoute.TryGetValue(BuildTenantRouteKey(tenantId, context.HttpMethod, context.Path), out policy))
                return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
            if (byTenant.TryGetValue(tenantId, out policy))
                return ValueTask.FromResult<KyrolusCachePolicy?>(policy);
        }

        if (byEntityEndpoint.TryGetValue((context.EntityType, context.Endpoint), out var entityPolicy))
            return ValueTask.FromResult<KyrolusCachePolicy?>(entityPolicy);

        if (byEndpoint.TryGetValue(context.Endpoint, out var endpointPolicy))
            return ValueTask.FromResult<KyrolusCachePolicy?>(endpointPolicy);

        if (byRoute.TryGetValue(BuildRouteKey(context.HttpMethod, context.Path), out var routePolicy))
            return ValueTask.FromResult<KyrolusCachePolicy?>(routePolicy);

        if (byEntity.TryGetValue(context.EntityType, out var typePolicy))
            return ValueTask.FromResult<KyrolusCachePolicy?>(typePolicy);

        return ValueTask.FromResult(defaultPolicy);
    }

    private static string BuildRouteKey(string httpMethod, string path)
        => $"{httpMethod.Trim().ToUpperInvariant()}:{path.Trim().Trim('/')}";

    private static string BuildTenantRouteKey(string tenantId, string httpMethod, string path)
        => $"{tenantId.Trim()}:{httpMethod.Trim().ToUpperInvariant()}:{path.Trim().Trim('/')}";
}
