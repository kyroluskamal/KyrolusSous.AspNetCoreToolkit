using System.Security.Authentication;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Abstractions.Models;
using KyrolusSous.RabbitMQ.Runtime.Health;
using KyrolusSous.RabbitMQ.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RabbitMQ.Client;

namespace KyrolusSous.RabbitMQ.Runtime.Config
{
    /// <summary>
    /// Service collection extensions for configuring Kyrolus RabbitMQ messaging and background consumers.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds and configures Kyrolus RabbitMQ with connection, publisher, and listener utilities.
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

            services.TryAddSingleton<IKyrolusRabbitMQConnection, KyrolusRabbitMQConnection>();
            services.TryAddSingleton<global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQConnection>(sp =>
                (global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQConnection)sp.GetRequiredService<IKyrolusRabbitMQConnection>());

            services.TryAddSingleton<IKyrolusRabbitMQUtils, KyrolusRabbitMQUtils>();
            services.TryAddSingleton<global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQUtils>(sp =>
                (global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQUtils)sp.GetRequiredService<IKyrolusRabbitMQUtils>());

            services.TryAddSingleton<IKyrolusRabbitMqListener, KyrolusRabbitMqListener>();
            services.TryAddSingleton<global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMqListener>(sp =>
                (global::KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMqListener)sp.GetRequiredService<IKyrolusRabbitMqListener>());

            return services;
        }

        /// <summary>
        /// Registers a strongly-typed RabbitMQ consumer background service.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQConsumer<TConsumer, TMessage>(
            this IServiceCollection services,
            Action<KyrolusRabbitMQConsumerOptions>? configure = null)
            where TConsumer : class, IKyrolusRabbitMQConsumer<TMessage>
            where TMessage : class
        {
            var options = new KyrolusRabbitMQConsumerOptions
            {
                QueueName = typeof(TMessage).Name.ToLowerInvariant()
            };
            configure?.Invoke(options);

            services.TryAddScoped<TConsumer>();
            services.AddHostedService(sp =>
                new KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage>(
                    sp.GetRequiredService<IKyrolusRabbitMQConnection>(),
                    sp,
                    options,
                    sp.GetService<Microsoft.Extensions.Logging.ILogger<KyrolusRabbitMQConsumerBackgroundService<TConsumer, TMessage>>>()));

            return services;
        }

        /// <summary>
        /// Adds the RabbitMQ RPC Client to dependency injection.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQRpcClient(this IServiceCollection services)
        {
            services.TryAddSingleton<IKyrolusRabbitMQRpcClient, KyrolusRabbitMQRpcClient>();
            return services;
        }

        /// <summary>
        /// Adds ASP.NET Core Health Checks for RabbitMQ broker connectivity.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQHealthCheck(
            this IServiceCollection services,
            string name = "rabbitmq",
            Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
            IEnumerable<string>? tags = null)
        {
            services.TryAddTransient<KyrolusRabbitMQHealthCheck>();
            services.AddHealthChecks().AddCheck<KyrolusRabbitMQHealthCheck>(
                name: name,
                failureStatus: failureStatus,
                tags: tags ?? ["ready", "live", "messaging", "rabbitmq"]);

            return services;
        }

        /// <summary>
        /// Adds Transactional Outbox support with a background publisher worker.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQOutbox(
            this IServiceCollection services,
            TimeSpan? pollInterval = null)
        {
            services.TryAddSingleton<Abstractions.Outbox.IKyrolusOutboxStore, Outbox.KyrolusInMemoryOutboxStore>();
            services.AddHostedService(sp => new Outbox.KyrolusOutboxPublisherWorker(
                sp.GetRequiredService<Abstractions.Outbox.IKyrolusOutboxStore>(),
                sp.GetRequiredService<IKyrolusRabbitMQUtils>(),
                sp.GetService<Microsoft.Extensions.Logging.ILogger<Outbox.KyrolusOutboxPublisherWorker>>(),
                pollInterval));

            return services;
        }

        /// <summary>
        /// Adds message idempotency and deduplication store.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQIdempotency(this IServiceCollection services)
        {
            services.TryAddSingleton<Abstractions.Idempotency.IKyrolusIdempotencyStore, Idempotency.KyrolusInMemoryIdempotencyStore>();
            return services;
        }

        /// <summary>
        /// Registers a fluent RabbitMQ topology builder.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQTopology(
            this IServiceCollection services,
            Action<Abstractions.Topology.IKyrolusRabbitMQTopologyBuilder>? configure = null)
        {
            var builder = new Topology.KyrolusRabbitMQTopologyBuilder();
            configure?.Invoke(builder);

            services.TryAddSingleton<Abstractions.Topology.IKyrolusRabbitMQTopologyBuilder>(builder);
            return services;
        }

        /// <summary>
        /// Adds AES-GCM payload encryption for RabbitMQ messages.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQEncryption(
            this IServiceCollection services,
            byte[] encryptionKey)
        {
            services.TryAddSingleton<Abstractions.Security.IKyrolusMessageEncryptor>(new Security.KyrolusAesMessageEncryptor(encryptionKey));
            return services;
        }

        /// <summary>
        /// Adds Gzip payload compression for RabbitMQ messages.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQCompression(this IServiceCollection services)
        {
            services.TryAddSingleton<Abstractions.Compression.IKyrolusMessageCompressor, Compression.KyrolusGzipMessageCompressor>();
            return services;
        }

        /// <summary>
        /// Registers a distributed Saga process manager and state store.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQSaga<TSaga, TState>(this IServiceCollection services)
            where TSaga : class, Abstractions.Sagas.IKyrolusSaga<TState>
            where TState : class, Abstractions.Sagas.IKyrolusSagaState
        {
            services.TryAddSingleton<Abstractions.Sagas.IKyrolusSagaStore<TState>, Sagas.KyrolusInMemorySagaStore<TState>>();
            services.TryAddScoped<TSaga>();
            services.TryAddScoped<Abstractions.Sagas.IKyrolusSaga<TState>, TSaga>();
            return services;
        }

        /// <summary>
        /// Adds the Dead Letter Queue (DLQ) inspection and replay manager.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQDlqManager(this IServiceCollection services)
        {
            services.TryAddSingleton<Abstractions.Dlq.IKyrolusDlqManager, Dlq.KyrolusDlqManager>();
            return services;
        }

        /// <summary>
        /// Adds the Message Upcaster registry for schema evolution.
        /// </summary>
        public static IServiceCollection AddKyrolusRabbitMQUpcasters(
            this IServiceCollection services,
            Action<Evolution.KyrolusMessageUpcasterRegistry>? configure = null)
        {
            var registry = new Evolution.KyrolusMessageUpcasterRegistry();
            configure?.Invoke(registry);

            services.TryAddSingleton(registry);
            return services;
        }

        /// <summary>
        /// Backward-compatibility overload for adding RabbitMQ with host, username, password.
        /// </summary>
        public static IServiceCollection AddRabbitMQ(
            this IServiceCollection services,
            string hostName,
            string userName,
            string password,
            int sslPort = 5671,
            int port = 5672,
            string? sslServerName = null)
        {
            return services.AddKyrolusRabbitMQ(options =>
            {
                options.HostName = hostName;
                options.UserName = userName;
                options.Password = password;
                options.Port = sslPort;
                options.SslEnabled = true;
                options.SslServerName = sslServerName ?? hostName;
            });
        }
    }
}

namespace KyrolusSous.RabbitMQUtils.Config
{
    /// <summary>
    /// Backward-compatibility extension methods.
    /// </summary>
    public static class RabbitMQExtensions
    {
        public static IServiceCollection AddKyrolusRabbitMQ(
            this IServiceCollection services,
            Action<global::KyrolusSous.RabbitMQ.Abstractions.Models.KyrolusRabbitMQOptions>? configure = null)
        {
            return global::KyrolusSous.RabbitMQ.Runtime.Config.ServiceCollectionExtensions.AddKyrolusRabbitMQ(services, configure);
        }

        public static IServiceCollection AddRabbitMQ(
            this IServiceCollection services,
            string hostName,
            string userName,
            string password,
            int sslPort = 5671,
            int port = 5672,
            string? sslServerName = null)
        {
            return global::KyrolusSous.RabbitMQ.Runtime.Config.ServiceCollectionExtensions.AddRabbitMQ(services, hostName, userName, password, sslPort, port, sslServerName);
        }
    }
}
