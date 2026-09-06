namespace KyrolusSous.Gateway.Yarp.Extensions;

/// <summary>
/// Extension methods for registering Kyrolus YARP Gateway services and security transform providers in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers Kyrolus YARP Gateway reverse proxy services using programmatic C# fluent configuration.
    /// </summary>
    /// <param name="services">The service collection to add gateway services to.</param>
    /// <param name="configure">An optional delegate for configuring clusters, destinations, and routes using <see cref="KyrolusDynamicInMemoryRouteConfigProvider"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>Registered Components:</b><br/>
    /// <list type="bullet">
    /// <item><description><see cref="IProxyConfigProvider"/> and <see cref="IKyrolusDynamicRouteProvider"/> (singleton in-memory provider).</description></item>
    /// <item><description><see cref="KyrolusCorrelationTransformProvider"/>: Distributed tracing (<c>X-Correlation-ID</c>).</description></item>
    /// <item><description><see cref="KyrolusSecurityHeadersTransformProvider"/>: Edge protection headers (<c>nosniff</c>, <c>DENY</c>).</description></item>
    /// <item><description><see cref="KyrolusTenantRoutingTransformProvider"/>: Multi-tenant resolution with reserved subdomains filtering.</description></item>
    /// <item><description><see cref="KyrolusRateLimitTransformProvider"/>: Gateway telemetry header injection.</description></item>
    /// <item><description>YARP core reverse proxy engine via <c>AddReverseProxy()</c>.</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Program.cs:
    /// builder.Services.AddKyrolusYarpGateway(gateway =>
    /// {
    ///     gateway.AddCluster("orders-service", cluster =>
    ///     {
    ///         cluster.WithLoadBalancing(KyrolusLoadBalancingPolicies.RoundRobin)
    ///                .AddDestination("node1", "http://10.0.1.10:5000")
    ///                .AddRoute("orders-route", "/api/orders/{**catch-all}");
    ///     });
    /// });
    /// 
    /// var app = builder.Build();
    /// app.MapReverseProxy();
    /// </code>
    /// </example>
    public static IServiceCollection AddKyrolusYarpGateway(
        this IServiceCollection services,
        Action<KyrolusDynamicInMemoryRouteConfigProvider>? configure = null)
    {
        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();
        configure?.Invoke(provider);

        services.AddSingleton<IProxyConfigProvider>(provider);
        services.AddSingleton<IKyrolusDynamicRouteProvider>(provider);
        services.AddSingleton<ITransformProvider, KyrolusCorrelationTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusSecurityHeadersTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusTenantRoutingTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusRateLimitTransformProvider>();
        services.AddReverseProxy();

        return services;
    }

    /// <summary>
    /// Registers Kyrolus YARP Gateway, loading routes and clusters from the specified <see cref="IConfiguration"/> section
    /// (e.g. <c>"ReverseProxy"</c> in appsettings.json), with support for hybrid programmatic cluster customizations.
    /// </summary>
    /// <param name="services">The service collection to add gateway services to.</param>
    /// <param name="configuration">The application configuration root containing the gateway configuration section.</param>
    /// <param name="sectionName">The name of the configuration section to load. Defaults to <c>"ReverseProxy"</c>.</param>
    /// <param name="configure">An optional programmatic callback to append or override routes/clusters defined in JSON.</param>
    /// <returns>The service collection for method chaining.</returns>
    /// <remarks>
    /// <para>
    /// <b>Hybrid Mode Support:</b><br/>
    /// This overload allows you to load baseline routes and clusters from <c>appsettings.json</c> while simultaneously
    /// adding dynamic, programmatic clusters in code using the fluent builder without conflict.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // In Program.cs (Hybrid mode):
    /// builder.Services.AddKyrolusYarpGateway(builder.Configuration, "ReverseProxy", gateway =>
    /// {
    ///     // Extra programmatic cluster added on top of appsettings.json:
    ///     gateway.AddCluster("emergency-fallback", c =>
    ///     {
    ///         c.AddDestination("node", "https://backup-service.internal")
    ///          .AddRoute("fallback-route", "/fallback");
    ///     });
    /// });
    /// </code>
    /// </example>
    public static IServiceCollection AddKyrolusYarpGateway(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = "ReverseProxy",
        Action<KyrolusDynamicInMemoryRouteConfigProvider>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var provider = new KyrolusDynamicInMemoryRouteConfigProvider();

        var section = configuration.GetSection(sectionName);
        if (section.Exists())
        {
            provider.LoadFromConfiguration(section);
        }

        configure?.Invoke(provider);

        services.AddSingleton<IProxyConfigProvider>(provider);
        services.AddSingleton<IKyrolusDynamicRouteProvider>(provider);
        services.AddSingleton<ITransformProvider, KyrolusCorrelationTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusSecurityHeadersTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusTenantRoutingTransformProvider>();
        services.AddSingleton<ITransformProvider, KyrolusRateLimitTransformProvider>();
        services.AddReverseProxy();

        return services;
    }
}
