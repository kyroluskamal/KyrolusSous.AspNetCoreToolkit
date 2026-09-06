namespace KyrolusSous.Gateway.Yarp.Configuration;

/// <summary>
/// Immutable snapshot implementation of YARP's <see cref="IProxyConfig"/> used to supply in-memory routing and cluster state to the reverse proxy engine.
/// </summary>
/// <param name="routes">The list of active YARP route configurations.</param>
/// <param name="clusters">The list of active YARP cluster configurations.</param>
/// <remarks>
/// <para>
/// <b>Role in YARP Architecture:</b><br/>
/// YARP's reverse proxy engine queries <see cref="IProxyConfigProvider.GetConfig"/> which returns an instance of <see cref="IProxyConfig"/>.
/// This class holds the frozen snapshot of routes and clusters, alongside the <see cref="ChangeToken"/> used by YARP to listen for configuration reloads.
/// </para>
/// </remarks>
internal sealed class KyrolusCustomProxyConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters) : IProxyConfig
{
    /// <summary>
    /// Gets the frozen snapshot of active YARP route configurations.
    /// </summary>
    public IReadOnlyList<RouteConfig> Routes { get; } = routes;

    /// <summary>
    /// Gets the frozen snapshot of active YARP cluster configurations.
    /// </summary>
    public IReadOnlyList<ClusterConfig> Clusters { get; } = clusters;

    /// <summary>
    /// Gets the change token that triggers when configuration updates occur.
    /// </summary>
    public IChangeToken ChangeToken { get; } = new CancellationChangeToken(CancellationToken.None);
}
