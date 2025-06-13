using InfrastructureServices.Services.Cache;
using KyrolusSous.RedisCaching.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace KyrolusSous.RedisCaching.Config;

public static class RedisCachingExtensions
{
    public static void AddRedisCachingConfig(this IServiceCollection services, IConfiguration configuration,
    string instanceName = "Redis:InstanceName", EndPointCollection? endPoints = null, string sslHost = "redis")
    {
        var options = new ConfigurationOptions
        {
            AbortOnConnectFail = false,
            ConnectRetry = 5,
            SyncTimeout = 10000,
            Ssl = true,
            SslHost = sslHost
        };

        if (endPoints != null)
        {
            foreach (var endpoint in endPoints)
            {
                options.EndPoints.Add(endpoint);
            }
        }
        else
        {
            options.EndPoints.Add("redis:6379");
        }

        services.AddSingleton<IConnectionMultiplexer>(sp =>
        {
            var connection = ConnectionMultiplexer.Connect(options);

            connection.ConnectionFailed += (sender, args) => Console.WriteLine($"Redis connection failed: {args.Exception!.Message}");

            connection.ConnectionRestored += (sender, args) => Console.WriteLine("Redis connection restored");
            return connection;
        });

        services.AddStackExchangeRedisCache(cacheOptions =>
        {
            cacheOptions.ConfigurationOptions = options;
            cacheOptions.InstanceName = configuration[instanceName];
        });

        services.AddScoped<ICacheService, RedisCacheService>();
    }

}
