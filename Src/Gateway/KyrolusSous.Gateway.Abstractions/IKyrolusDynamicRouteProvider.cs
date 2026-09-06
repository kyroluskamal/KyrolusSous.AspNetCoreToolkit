namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Defines the contract for querying, managing, and dynamically reloading API Gateway routes and clusters at runtime
/// without requiring an application restart (Zero-Downtime reconfiguration).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dynamic Routing Concept:</b><br/>
/// In cloud-native and microservice architectures, backend service instances may scale up or down dynamically,
/// and new routes may need to be published at runtime. Implementing this interface allows the Gateway to query
/// the current routing topology and trigger hot-reloads on demand.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Injecting and querying the dynamic route provider in an admin controller or background worker:
/// public class GatewayAdminService(IKyrolusDynamicRouteProvider routeProvider)
/// {
///     public async Task RefreshGatewayAsync()
///     {
///         var activeRoutes = await routeProvider.GetRoutesAsync();
///         var activeClusters = await routeProvider.GetClustersAsync();
///         
///         // Signal the reverse proxy to reload its configuration pipeline:
///         await routeProvider.ReloadAsync();
///     }
/// }
/// </code>
/// </example>
public interface IKyrolusDynamicRouteProvider
{
    /// <summary>
    /// Asynchronously retrieves the snapshot of all currently configured gateway routes.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of active <see cref="KyrolusGatewayRoute"/> instances.</returns>
    Task<IReadOnlyList<KyrolusGatewayRoute>> GetRoutesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously retrieves the snapshot of all currently configured backend service clusters and their destination replicas.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A read-only list of active <see cref="KyrolusGatewayCluster"/> instances.</returns>
    Task<IReadOnlyList<KyrolusGatewayCluster>> GetClustersAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Signals the gateway engine to reload its routing and cluster configurations, notifying YARP to rebuild its proxy pipeline.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous reload operation.</returns>
    Task ReloadAsync(CancellationToken cancellationToken = default);
}
