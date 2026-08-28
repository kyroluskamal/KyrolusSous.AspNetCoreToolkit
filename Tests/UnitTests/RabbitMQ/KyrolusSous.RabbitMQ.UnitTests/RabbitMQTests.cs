using System.Diagnostics;
using KyrolusSous.RabbitMQ.Abstractions.Interfaces;
using KyrolusSous.RabbitMQ.Abstractions.Models;
using KyrolusSous.RabbitMQ.Runtime.Config;
using KyrolusSous.RabbitMQ.Runtime.Diagnostics;
using KyrolusSous.RabbitMQ.Runtime.Health;
using KyrolusSous.RabbitMQ.Runtime.Models;
using KyrolusSous.RabbitMQ.Runtime.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Shouldly;
using Xunit;

namespace KyrolusSous.RabbitMQ.UnitTests;

public class RabbitMQTests
{
    [Fact]
    public void Options_HaveSafeDefaults()
    {
        var options = new KyrolusRabbitMQOptions();

        options.HostName.ShouldBe("localhost");
        options.Port.ShouldBe(5672);
        options.UserName.ShouldBe("guest");
        options.Password.ShouldBe("guest");
        options.VirtualHost.ShouldBe("/");
        options.SslEnabled.ShouldBeFalse();
        options.PrefetchCount.ShouldBe((ushort)50);
        options.MaxRetryAttempts.ShouldBe(3);
        options.UseDeadLetterExchange.ShouldBeTrue();
        options.DlxExchangeName.ShouldBe("dlx.exchange");
        options.EnablePublisherConfirms.ShouldBeTrue();
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

        queue.ShouldBeAssignableTo<IKyrolusQueueSetup>();
        queue.ShouldBeAssignableTo<KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IQueueSetup>();
        queue.Name.ShouldBe("orders-queue");
        queue.RoutingKey.ShouldBe("orders.created");
    }

    [Fact]
    public void BackwardCompatibility_QueueSetupAliasWorks()
    {
        var queue = new KyrolusSous.RabbitMQUtils.Models.QueueSetup
        {
            Name = "legacy-queue",
            RoutingKey = "legacy.key"
        };

        queue.ShouldBeAssignableTo<IKyrolusQueueSetup>();
        queue.ShouldBeAssignableTo<KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IQueueSetup>();
    }

    [Fact]
    public void MessageEnvelope_InitializesWithCorrelationAndTimestamp()
    {
        var payload = new { OrderId = 123, Total = 450.50 };
        var envelope = new KyrolusMessageEnvelope<object>(payload, correlationId: "corr-999", causationId: "cause-888");

        envelope.MessageId.ShouldNotBeNull();
        envelope.CorrelationId.ShouldBe("corr-999");
        envelope.CausationId.ShouldBe("cause-888");
        envelope.Payload.ShouldBe(payload);
        envelope.Timestamp.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
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
        options.ShouldNotBeNull();
        options.HostName.ShouldBe("custom-broker");
        options.Port.ShouldBe(5673);
        options.UserName.ShouldBe("admin");

        var factory = provider.GetService<IConnectionFactory>();
        factory.ShouldNotBeNull();

        var kyrolusConn = provider.GetService<IKyrolusRabbitMQConnection>();
        kyrolusConn.ShouldNotBeNull();

        var legacyConn = provider.GetService<KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQConnection>();
        legacyConn.ShouldNotBeNull();

        var kyrolusUtils = provider.GetService<IKyrolusRabbitMQUtils>();
        kyrolusUtils.ShouldNotBeNull();

        var legacyUtils = provider.GetService<KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMQUtils>();
        legacyUtils.ShouldNotBeNull();

        var kyrolusListener = provider.GetService<IKyrolusRabbitMqListener>();
        kyrolusListener.ShouldNotBeNull();

        var legacyListener = provider.GetService<KyrolusSous.IRabbitMQUtilsInterfaces.Interfaces.IRabbitMqListener>();
        legacyListener.ShouldNotBeNull();
    }

    [Fact]
    public void DiRegistration_LegacyAddRabbitMQ_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRabbitMQ("my-host", "my-user", "my-pwd", 5671, 5672);

        var provider = services.BuildServiceProvider();
        var options = provider.GetService<KyrolusRabbitMQOptions>();

        options.ShouldNotBeNull();
        options.HostName.ShouldBe("my-host");
        options.UserName.ShouldBe("my-user");
        options.Password.ShouldBe("my-pwd");
        options.Port.ShouldBe(5671);
        options.SslEnabled.ShouldBeTrue();
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

        restored.ShouldNotBeNull();
        restored.Payload.ShouldBe("test-payload");
        restored.CorrelationId.ShouldBe("c-123");
        restored.CausationId.ShouldBe("causation-456");
        restored.Headers["tenant-id"].ShouldBe("tenant-001");
        restored.Headers["environment"].ShouldBe("staging");
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

        options.RequestedHeartbeat.ShouldBe(TimeSpan.FromSeconds(30));
        options.MaxRetryAttempts.ShouldBe(5);
        options.RetryInitialDelay.ShouldBe(TimeSpan.FromMilliseconds(500));
        options.RetryBackoffMultiplier.ShouldBe(3.0);
        options.DlxExchangeName.ShouldBe("custom.dlx");
        options.DlxRoutingKeyPrefix.ShouldBe("dead.");
    }

    #region Enterprise Features Tests

    [Fact]
    public void ConsumerOptions_DefaultsAndConfiguresCorrectly()
    {
        var options = new KyrolusRabbitMQConsumerOptions
        {
            QueueName = "orders-processing",
            ExchangeName = "orders.exchange",
            RoutingKey = "orders.#",
            PrefetchCount = 25,
            MaxRetries = 5,
            RetryDelay = TimeSpan.FromSeconds(2),
            UseDeadLetterOnFailure = true
        };

        options.QueueName.ShouldBe("orders-processing");
        options.ExchangeName.ShouldBe("orders.exchange");
        options.RoutingKey.ShouldBe("orders.#");
        options.PrefetchCount.ShouldBe((ushort)25);
        options.MaxRetries.ShouldBe(5);
        options.RetryDelay.ShouldBe(TimeSpan.FromSeconds(2));
        options.UseDeadLetterOnFailure.ShouldBeTrue();
    }

    [Fact]
    public void Instrumentation_InjectAndExtractTraceContext_RoundtripsSuccessfully()
    {
        using var activity = new Activity("TestActivity").Start();
        var headers = new Dictionary<string, object?>();

        KyrolusRabbitMQInstrumentation.InjectTraceContext(headers, activity);

        headers.ShouldContainKey(KyrolusRabbitMQInstrumentation.TraceParentHeader);
        headers[KyrolusRabbitMQInstrumentation.TraceParentHeader].ShouldBe(activity.Id);

        var extractedContext = KyrolusRabbitMQInstrumentation.ExtractTraceContext(headers);
        extractedContext.TraceId.ShouldBe(activity.TraceId);
        extractedContext.SpanId.ShouldBe(activity.SpanId);
    }

    [Fact]
    public void DiRegistration_AddConsumerAndRpcClient_RegistersExpectedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRabbitMQ(options =>
        {
            options.HostName = "localhost";
        });
        services.AddKyrolusRabbitMQRpcClient();
        services.AddKyrolusRabbitMQHealthCheck();
        services.AddKyrolusRabbitMQConsumer<TestOrderCreatedConsumer, TestOrderCreatedEvent>(opt =>
        {
            opt.QueueName = "orders.test.queue";
            opt.PrefetchCount = 15;
        });

        var provider = services.BuildServiceProvider();

        var rpcClient = provider.GetService<IKyrolusRabbitMQRpcClient>();
        rpcClient.ShouldNotBeNull();

        var healthCheck = provider.GetService<KyrolusRabbitMQHealthCheck>();
        healthCheck.ShouldNotBeNull();

        var hostedServices = provider.GetServices<IHostedService>();
        hostedServices.ShouldContain(s => s is KyrolusRabbitMQConsumerBackgroundService<TestOrderCreatedConsumer, TestOrderCreatedEvent>);
    }

    [Fact]
    public void ConsumeContext_Record_HoldsAllProperties()
    {
        var headers = new Dictionary<string, object?> { ["custom-header"] = "custom-value" };
        var context = new KyrolusRabbitMQConsumeContext(
            Exchange: "events.exchange",
            RoutingKey: "event.created",
            DeliveryTag: 12345,
            Redelivered: true,
            MessageId: "msg-001",
            CorrelationId: "corr-001",
            TraceParent: "00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01",
            Headers: headers);

        context.Exchange.ShouldBe("events.exchange");
        context.RoutingKey.ShouldBe("event.created");
        context.DeliveryTag.ShouldBe((ulong)12345);
        context.Redelivered.ShouldBeTrue();
        context.MessageId.ShouldBe("msg-001");
        context.CorrelationId.ShouldBe("corr-001");
        context.TraceParent.ShouldBe("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01");
        context.Headers["custom-header"].ShouldBe("custom-value");
    }

    #endregion
}

public sealed record TestOrderCreatedEvent(int OrderId, decimal Amount);

public sealed class TestOrderCreatedConsumer : IKyrolusRabbitMQConsumer<TestOrderCreatedEvent>
{
    public Task HandleAsync(TestOrderCreatedEvent message, KyrolusRabbitMQConsumeContext context, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
