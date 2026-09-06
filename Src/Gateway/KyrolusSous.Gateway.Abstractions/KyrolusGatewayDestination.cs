namespace KyrolusSous.Gateway.Abstractions;

/// <summary>
/// Represents a single physical backend destination endpoint (replica) belonging to a service cluster.
/// </summary>
/// <param name="Address">
/// The absolute base URI of the target backend service replica (e.g., <c>http://10.0.1.20:5001</c> or <c>https://orders-service:5001</c>).
/// </param>
/// <remarks>
/// <para>
/// <b>What is a Destination?</b><br/>
/// A <b>Destination</b> is <i>NEVER</i> the client's incoming domain (e.g., <c>api.mycompany.com</c>) and is <i>NEVER</i> a URL path inside the service (e.g., <c>/api/orders</c>).
/// Instead, it is the <b>physical network address (Internal Hostname/IP and Port)</b> of a specific running instance or container of your backend microservice.
/// </para>
/// <para>
/// <b>Use Case:</b><br/>
/// If you have an "Invoices Service" scaled horizontally across 3 Docker containers or VMs, your cluster will contain 3 destinations:
/// <list type="bullet">
/// <item><description><c>srv-1</c> -&gt; <c>http://10.0.2.10:5000</c></description></item>
/// <item><description><c>srv-2</c> -&gt; <c>http://10.0.2.11:5000</c></description></item>
/// <item><description><c>srv-3</c> -&gt; <c>http://10.0.2.12:5000</c></description></item>
/// </list>
/// When an external request arrives at the Gateway, the load balancer selects one of these destinations to proxy the HTTP request to.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // Registering a destination manually:
/// var destination = new KyrolusGatewayDestination("https://internal-billing-node1:8443");
/// 
/// // Or using the fluent cluster builder:
/// cluster.AddDestination("node1", "http://10.0.1.50:5000");
/// </code>
/// </example>
public sealed record KyrolusGatewayDestination(string Address);
