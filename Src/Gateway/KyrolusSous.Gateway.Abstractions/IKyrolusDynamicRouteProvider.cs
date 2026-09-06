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

    /// <summary>
    /// Asynchronously removes a gateway route by its identifier and triggers a reload if found.
    /// </summary>
    /// <param name="routeId">The unique identifier of the route to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the route was found and removed; otherwise, <c>false</c>.</returns>
    Task<bool> RemoveRouteAsync(string routeId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a gateway cluster by its identifier and triggers a reload if found.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the cluster to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the cluster was found and removed; otherwise, <c>false</c>.</returns>
    Task<bool> RemoveClusterAsync(string clusterId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds or updates a gateway route and signals a dynamic reload.
    /// </summary>
    /// <param name="route">The gateway route to add or replace.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddRouteAsync(KyrolusGatewayRoute route, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds or updates a gateway service cluster and signals a dynamic reload.
    /// </summary>
    /// <param name="cluster">The gateway cluster to add or replace.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddClusterAsync(KyrolusGatewayCluster cluster, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds or updates multiple gateway routes in an atomic batch and signals a single dynamic reload.
    /// </summary>
    /// <param name="routes">The collection of routes to add or replace.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddRoutesAsync(IEnumerable<KyrolusGatewayRoute> routes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously adds or updates multiple gateway clusters in an atomic batch and signals a single dynamic reload.
    /// </summary>
    /// <param name="clusters">The collection of clusters to add or replace.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddClustersAsync(IEnumerable<KyrolusGatewayCluster> clusters, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously removes a specific backend destination endpoint from a cluster (node decommissioning / draining) and signals a reload.
    /// </summary>
    /// <param name="clusterId">The unique identifier of the target cluster.</param>
    /// <param name="destinationId">The unique identifier of the destination node to remove.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns><c>true</c> if the destination was found and removed; otherwise, <c>false</c>.</returns>
    Task<bool> RemoveDestinationAsync(string clusterId, string destinationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously clears all routes and clusters from in-memory state and signals a dynamic reload.
    /// </summary>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);
}
