using System.Security.Authentication;
using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using KyrolusSous.IRabbitMQUtilsInterfaces.Models;
using KyrolusSous.RabbitMQUtils.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQUtils.Config;

/// <summary>
/// Service collection extensions for configuring Kyrolus RabbitMQ messaging.
/// </summary>
public static class RabbitMQExtensions
{
    /// <summary>
    /// Adds and configures Kyrolus RabbitMQ with options.
    /// </summary>
    public static IServiceCollection AddKyrolusRabbitMQ(
        this IServiceCollection services,
        Action<KyrolusRabbitMQOptions>? configure = null)
    {
        var options = new KyrolusRabbitMQOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);

        services.TryAddSingleton<IConnectionFactory>(sp =>
        {
            var opt = sp.GetService<KyrolusRabbitMQOptions>() ?? options;
            var factory = new ConnectionFactory
            {
                HostName = opt.HostName,
                Port = opt.Port,
                UserName = opt.UserName,
                Password = opt.Password,
                VirtualHost = opt.VirtualHost,
                RequestedHeartbeat = opt.RequestedHeartbeat,
                NetworkRecoveryInterval = opt.NetworkRecoveryInterval,
                AutomaticRecoveryEnabled = opt.AutomaticRecoveryEnabled,
            };

            if (opt.SslEnabled)
            {
                factory.Ssl = new SslOption
                {
                    Enabled = true,
                    ServerName = opt.SslServerName ?? opt.HostName,
                    Version = SslProtocols.Tls12 | SslProtocols.Tls13
                };
            }

            return factory;
        });

        services.TryAddSingleton<IKyrolusRabbitMQConnection>(sp =>
        {
            var factory = sp.GetRequiredService<IConnectionFactory>();
            return new RabbitMQConnection(factory);
        });

        services.TryAddSingleton<IRabbitMQConnection>(sp =>
            (RabbitMQConnection)sp.GetRequiredService<IKyrolusRabbitMQConnection>());

        services.TryAddScoped<IKyrolusRabbitMQUtils, Services.RabbitMQUtils>();
        services.TryAddScoped<IRabbitMQUtils>(sp =>
            (Services.RabbitMQUtils)sp.GetRequiredService<IKyrolusRabbitMQUtils>());

        services.TryAddScoped<IKyrolusRabbitMqListener, RabbitMqListener>();
        services.TryAddScoped<IRabbitMqListener>(sp =>
            (RabbitMqListener)sp.GetRequiredService<IKyrolusRabbitMqListener>());

        return services;
    }

    /// <summary>
    /// Backward-compatibility overload for <see cref="AddKyrolusRabbitMQ"/>.
    /// </summary>
    public static IServiceCollection AddRabbitMQ(
        this IServiceCollection services,
        string hostName = "localhost",
        string userName = "guest",
        string password = "guest",
        int? sslPort = 5671,
        int httpPort = 5672)
    {
        return services.AddKyrolusRabbitMQ(options =>
        {
            options.HostName = hostName;
            options.UserName = userName;
            options.Password = password;
            options.Port = sslPort ?? httpPort;
            options.SslEnabled = sslPort.HasValue;
        });
    }
}
