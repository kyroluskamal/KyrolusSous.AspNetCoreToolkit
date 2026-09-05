using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;

namespace KyrolusSous.Gateway.Yarp;

/// <summary>
/// Custom implementation of <see cref="IProxyConfig"/> for holding in-memory gateway snapshot configs.
/// </summary>
internal sealed class KyrolusCustomProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig
{
    public IReadOnlyList<RouteConfig> Routes { get; } = routes;
    public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;
    public IChangeToken ChangeToken { get; } = new CancellationChangeToken(CancellationToken.None);
}
