using KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces;
using KyrolusSous.IRabbitMQUtilsInterfaces.Models;
using KyrolusSous.RabbitMQUtils.Config;
using KyrolusSous.RabbitMQUtils.Models;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace KyrolusSous.RabbitMQ.UnitTests;

public class RabbitMQTests
{
    [Fact]
    public void Options_HaveSafeDefaults()
    {
        var options = new KyrolusRabbitMQOptions();

        Assert.Equal("localhost", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("guest", options.UserName);
        Assert.Equal("guest", options.Password);
        Assert.Equal("/", options.VirtualHost);
        Assert.False(options.SslEnabled);
        Assert.Equal((ushort)50, options.PrefetchCount);
        Assert.Equal(3, options.MaxRetryAttempts);
        Assert.True(options.UseDeadLetterExchange);
        Assert.Equal("dlx.exchange", options.DlxExchangeName);
    }

    [Fact]
    public void QueueSetup_ImplementsBothInterfaces()
    {
        var queue = new KyrolusQueueSetup
        {
            Name = "orders-queue",
            RoutingKey = "orders.created",
            Durable = true,
            Exclusive = false,
            Autodelete = false
        };

        Assert.IsAssignableFrom<IKyrolusQueueSetup>(queue);
        Assert.IsAssignableFrom<IQueueSetup>(queue);
        Assert.Equal("orders-queue", queue.Name);
        Assert.Equal("orders.created", queue.RoutingKey);
    }

    [Fact]
    public void BackwardCompatibility_QueueSetupAliasWorks()
    {
        var queue = new QueueSetup
        {
            Name = "legacy-queue",
            RoutingKey = "legacy.key"
        };

        Assert.IsAssignableFrom<IKyrolusQueueSetup>(queue);
        Assert.IsAssignableFrom<IQueueSetup>(queue);
    }

    [Fact]
    public void MessageEnvelope_InitializesWithCorrelationAndTimestamp()
    {
        var payload = new { OrderId = 123, Total = 450.50 };
        var envelope = new KyrolusMessageEnvelope<object>(payload, correlationId: "corr-999", causationId: "cause-888");

        Assert.NotNull(envelope.MessageId);
        Assert.Equal("corr-999", envelope.CorrelationId);
        Assert.Equal("cause-888", envelope.CausationId);
        Assert.Equal(payload, envelope.Payload);
        Assert.True(envelope.Timestamp <= DateTimeOffset.UtcNow);
    }

    [Fact]
    public void DiRegistration_AddKyrolusRabbitMQ_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRabbitMQ(options =>
        {
            options.HostName = "custom-broker";
            options.Port = 5673;
            options.UserName = "admin";
            options.Password = "pass123";
        });

        var provider = services.BuildServiceProvider();

        var options = provider.GetService<KyrolusRabbitMQOptions>();
        Assert.NotNull(options);
        Assert.Equal("custom-broker", options.HostName);
        Assert.Equal(5673, options.Port);
        Assert.Equal("admin", options.UserName);

        var factory = provider.GetService<IConnectionFactory>();
        Assert.NotNull(factory);

        var kyrolusConn = provider.GetService<IKyrolusRabbitMQConnection>();
        Assert.NotNull(kyrolusConn);

        var legacyConn = provider.GetService<IRabbitMQConnection>();
        Assert.NotNull(legacyConn);

        var kyrolusUtils = provider.GetService<IKyrolusRabbitMQUtils>();
        Assert.NotNull(kyrolusUtils);

        var legacyUtils = provider.GetService<IRabbitMQUtils>();
        Assert.NotNull(legacyUtils);

        var kyrolusListener = provider.GetService<IKyrolusRabbitMqListener>();
        Assert.NotNull(kyrolusListener);

        var legacyListener = provider.GetService<IRabbitMqListener>();
        Assert.NotNull(legacyListener);
    }

    [Fact]
    public void DiRegistration_LegacyAddRabbitMQ_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRabbitMQ("my-host", "my-user", "my-pwd", 5671, 5672);

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<KyrolusRabbitMQOptions>();

        Assert.NotNull(options);
        Assert.Equal("my-host", options.HostName);
        Assert.Equal("my-user", options.UserName);
        Assert.Equal("my-pwd", options.Password);
        Assert.Equal(5671, options.Port);
        Assert.True(options.SslEnabled);
    }

    [Fact]
    public void MessageEnvelope_SerializesAndDeserializesCorrectly()
    {
        var original = new KyrolusMessageEnvelope<string>("test-payload", "c-123", "causation-456")
        {
            Headers = new Dictionary<string, string>
            {
                ["tenant-id"] = "tenant-001",
                ["environment"] = "staging"
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(original);
        var restored = System.Text.Json.JsonSerializer.Deserialize<KyrolusMessageEnvelope<string>>(json);

        Assert.NotNull(restored);
        Assert.Equal("test-payload", restored.Payload);
        Assert.Equal("c-123", restored.CorrelationId);
        Assert.Equal("causation-456", restored.CausationId);
        Assert.Equal("tenant-001", restored.Headers["tenant-id"]);
        Assert.Equal("staging", restored.Headers["environment"]);
    }

    [Fact]
    public void Options_AllowsCustomHeartbeatAndRetries()
    {
        var options = new KyrolusRabbitMQOptions
        {
            RequestedHeartbeat = TimeSpan.FromSeconds(30),
            MaxRetryAttempts = 5,
            RetryInitialDelay = TimeSpan.FromMilliseconds(500),
            RetryBackoffMultiplier = 3.0,
            UseDeadLetterExchange = true,
            DlxExchangeName = "custom.dlx",
            DlxRoutingKeyPrefix = "dead."
        };

        Assert.Equal(TimeSpan.FromSeconds(30), options.RequestedHeartbeat);
        Assert.Equal(5, options.MaxRetryAttempts);
        Assert.Equal(TimeSpan.FromMilliseconds(500), options.RetryInitialDelay);
        Assert.Equal(3.0, options.RetryBackoffMultiplier);
        Assert.Equal("custom.dlx", options.DlxExchangeName);
        Assert.Equal("dead.", options.DlxRoutingKeyPrefix);
    }
}
