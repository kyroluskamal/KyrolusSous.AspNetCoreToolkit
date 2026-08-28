using KyrolusSous.Caching.Abstractions;
using KyrolusSous.EndpointKit.EF.BaseKyrolusModule.Interfaces;

namespace KyrolusSous.EndpointKit.EF.BaseKyrolusModule;

public sealed class KyrolusEndpointCacheKeyContext(IKyrolusEndpointContext context) : IKyrolusCacheKeyContext
{
    public string? ScopeKey => context.ScopeKey;
    public string? TenantId => context.TenantId;
}
