using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.Marten.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.Marten.BaseKyrolusModule;

public sealed class KyrolusEndpointCacheKeyContext(IKyrolusEndpointContext context) : IKyrolusCacheKeyContext
{
    public string? ScopeKey => context.ScopeKey;
    public string? TenantId => context.TenantId;
}
