namespace KyrolusSous.Elasticsearch;

/// <summary>
/// Service collection extension methods for registering Kyrolus Elasticsearch services, repositories, caching decorators, and audit loggers.
/// </summary>
public static class ElasticsearchServiceExtensions
{
    public static WebApplicationBuilder AddKyrolusElasticsearch(
        this WebApplicationBuilder builder,
        Action<KyrolusElasticsearchOptions>? configureOptions = null)
    {
        var configSection = builder.Configuration.GetSection("KyrolusElasticsearch");
        builder.Services.AddKyrolusElasticsearch(configSection, configureOptions);
        return builder;
    }

    public static IServiceCollection AddKyrolusElasticsearch(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KyrolusElasticsearchOptions>? configureOptions = null)
    {
        var options = new KyrolusElasticsearchOptions();
        configuration.Bind(options);
        configureOptions?.Invoke(options);
        return RegisterElasticsearchServices(services, options);
    }

    public static IServiceCollection AddKyrolusElasticsearch(
        this IServiceCollection services,
        Action<KyrolusElasticsearchOptions>? configureOptions = null)
    {
        var options = new KyrolusElasticsearchOptions();
        configureOptions?.Invoke(options);
        return RegisterElasticsearchServices(services, options);
    }

    public static IServiceCollection AddElasticsearchTenantProvider<TTenantProvider>(this IServiceCollection services)
        where TTenantProvider : class, IKyrolusTenantProvider
    {
        services.AddScoped<IKyrolusTenantProvider, TTenantProvider>();
        return services;
    }

    public static IServiceCollection AddElasticsearchEfSync(this IServiceCollection services)
    {
        services.AddScoped<KyrolusElasticSyncInterceptor>();
        return services;
    }

    public static IServiceCollection AddKyrolusElasticsearchAuditLogging(this IServiceCollection services)
    {
        services.AddScoped<IKyrolusElasticsearchAuditLogger, KyrolusElasticsearchAuditLogger>();
        return services;
    }

    public static IServiceCollection AddKyrolusCachedElasticRepository<TDocument, TId>(
        this IServiceCollection services,
        TimeSpan? defaultTtl = null)
        where TDocument : class
    {
        services.AddScoped<KyrolusElasticRepository<TDocument, TId>>();
        services.AddScoped<IKyrolusElasticRepository<TDocument, TId>>(sp =>
        {
            var inner = sp.GetRequiredService<KyrolusElasticRepository<TDocument, TId>>();
            var cache = sp.GetService<KyrolusSous.Caching.Abstractions.IKyrolusCacheProvider>();
            return new KyrolusCachedElasticRepository<TDocument, TId>(inner, cache, defaultTtl);
        });

        return services;
    }

    public static IHealthChecksBuilder AddElasticsearchHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "elasticsearch",
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null)
    {
        return builder.AddCheck<KyrolusElasticsearchHealthCheck>(
            name,
            failureStatus,
            tags ?? ["db", "search", "elasticsearch"]);
    }

    private static IServiceCollection RegisterElasticsearchServices(
        IServiceCollection services,
        KyrolusElasticsearchOptions options)
    {
        services.AddSingleton(Options.Create(options));

        services.AddSingleton(sp =>
        {
            var opt = sp.GetRequiredService<IOptions<KyrolusElasticsearchOptions>>().Value;
            var settings = CreateClientSettings(opt);
            return new ElasticsearchClient(settings);
        });

        services.AddScoped<IKyrolusElasticIndexManager, KyrolusElasticIndexManager>();
        services.AddScoped(typeof(IKyrolusElasticRepository<,>), typeof(KyrolusElasticRepository<,>));
        services.AddScoped<KyrolusElasticSyncInterceptor>();

        if (options.AutoCreateIndices)
        {
            services.AddHostedService<KyrolusElasticsearchIndexInitializerHostedService>();
        }

        return services;
    }

    private static ElasticsearchClientSettings CreateClientSettings(KyrolusElasticsearchOptions options)
    {
        ElasticsearchClientSettings settings;

        if (options.NodeUrls.Count > 0)
        {
            var uris = options.NodeUrls.Select(u => new Uri(u)).ToList();
            var nodePool = new StaticNodePool(uris);
            settings = new ElasticsearchClientSettings(nodePool);
        }
        else
        {
            var uri = new Uri(options.Url);
            var nodePool = new SingleNodePool(uri);
            settings = new ElasticsearchClientSettings(nodePool);
        }

        if (!string.IsNullOrWhiteSpace(options.DefaultIndex))
        {
            settings.DefaultIndex(options.DefaultIndex);
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            settings.Authentication(new ApiKey(options.ApiKey));
        }
        else if (!string.IsNullOrWhiteSpace(options.Username) && !string.IsNullOrWhiteSpace(options.Password))
        {
            settings.Authentication(new BasicAuthentication(options.Username, options.Password));
        }

        if (!string.IsNullOrWhiteSpace(options.CertificateFingerprint))
        {
            settings.CertificateFingerprint(options.CertificateFingerprint);
        }

        if (options.ConnectionTimeoutSeconds > 0)
        {
            settings.RequestTimeout(TimeSpan.FromSeconds(options.ConnectionTimeoutSeconds));
        }

        if (options.MaxRetries > 0)
        {
            settings.MaximumRetries(options.MaxRetries);
        }

        if (options.EnableHttpCompression)
        {
            settings.EnableHttpCompression();
        }

        if (options.EnableDebugMode)
        {
            settings.EnableDebugMode();
        }

        return settings;
    }
}
