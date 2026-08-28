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

    [Fact]
    public async Task OutboxStore_AddAndRetrievePendingMessages_Successfully()
    {
        var store = new KyrolusSous.RabbitMQ.Runtime.Outbox.KyrolusInMemoryOutboxStore();
        var msg = new KyrolusSous.RabbitMQ.Abstractions.Outbox.KyrolusOutboxMessage
        {
            Exchange = "orders.exchange",
            RoutingKey = "order.created",
            Payload = "{\"OrderId\": 123}",
            MessageType = "OrderCreated"
        };

        await store.AddAsync(msg);
        var pending = await store.GetPendingMessagesAsync(10);

        pending.Count.ShouldBe(1);
        pending[0].Exchange.ShouldBe("orders.exchange");

        await store.MarkAsProcessedAsync(msg.Id);
        var afterProcessed = await store.GetPendingMessagesAsync(10);
        afterProcessed.Count.ShouldBe(0);
    }

    [Fact]
    public async Task IdempotencyStore_AcquireLockAndCacheResult_PreventsDuplicate()
    {
        var store = new KyrolusSous.RabbitMQ.Runtime.Idempotency.KyrolusInMemoryIdempotencyStore();
        var key = "idemp-key-100";

        var acquiredFirst = await store.TryAcquireLockAsync(key, TimeSpan.FromMinutes(1));
        acquiredFirst.ShouldBeTrue();

        var acquiredSecond = await store.TryAcquireLockAsync(key, TimeSpan.FromMinutes(1));
        acquiredSecond.ShouldBeFalse(); // Lock already held

        await store.SetResultAsync(key, "{\"status\": \"success\"}", TimeSpan.FromHours(1));

        var cachedResult = await store.GetResultAsync(key);
        cachedResult.ShouldBe("{\"status\": \"success\"}");
    }

    [Fact]
    public void AesMessageEncryptor_EncryptAndDecrypt_RoundtripsSuccessfully()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);

        var encryptor = new KyrolusSous.RabbitMQ.Runtime.Security.KyrolusAesMessageEncryptor(key);
        var originalText = "Sensitive Payload: Bank Account #1234-5678";
        var rawBytes = System.Text.Encoding.UTF8.GetBytes(originalText);

        var encryptedBytes = encryptor.Encrypt(rawBytes);
        encryptedBytes.ShouldNotBeNull();
        encryptedBytes.Length.ShouldBeGreaterThan(rawBytes.Length);

        var decryptedBytes = encryptor.Decrypt(encryptedBytes);
        var decryptedText = System.Text.Encoding.UTF8.GetString(decryptedBytes);

        decryptedText.ShouldBe(originalText);
    }

    [Fact]
    public void GzipMessageCompressor_CompressAndDecompress_RoundtripsSuccessfully()
    {
        var compressor = new KyrolusSous.RabbitMQ.Runtime.Compression.KyrolusGzipMessageCompressor();
        compressor.EncodingName.ShouldBe("gzip");

        var largeString = string.Join(",", Enumerable.Repeat("KyrolusSous Enterprise Messaging System Test Data", 100));
        var rawBytes = System.Text.Encoding.UTF8.GetBytes(largeString);

        var compressed = compressor.Compress(rawBytes);
        compressed.Length.ShouldBeLessThan(rawBytes.Length);

        var decompressed = compressor.Decompress(compressed);
        var restoredString = System.Text.Encoding.UTF8.GetString(decompressed);

        restoredString.ShouldBe(largeString);
    }

    [Fact]
    public void CircuitBreaker_TripsAfterThreshold_AndRecovers()
    {
        var breaker = new KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusConsumerCircuitBreaker(
            consecutiveFailureThreshold: 3,
            breakDuration: TimeSpan.FromMilliseconds(50));

        breaker.CanExecute().ShouldBeTrue();
        breaker.State.ShouldBe(KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusCircuitState.Closed);

        breaker.ReportFailure();
        breaker.ReportFailure();
        breaker.CanExecute().ShouldBeTrue();

        breaker.ReportFailure(); // 3rd failure -> Opens circuit
        breaker.State.ShouldBe(KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusCircuitState.Open);
        breaker.CanExecute().ShouldBeFalse();

        breaker.Reset();
        breaker.CanExecute().ShouldBeTrue();
        breaker.State.ShouldBe(KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusCircuitState.Closed);
    }

    [Fact]
    public void TopologyBuilder_FluentApi_ConfiguresExchangesAndQueues()
    {
        var builder = new KyrolusSous.RabbitMQ.Runtime.Topology.KyrolusRabbitMQTopologyBuilder();
        builder.AddExchange("orders.topic", global::RabbitMQ.Client.ExchangeType.Topic)
               .AddQueue("orders.processing.queue")
               .BindQueue("orders.processing.queue", "orders.topic", "orders.#");

        builder.Exchanges.Count.ShouldBe(1);
        builder.Exchanges[0].Name.ShouldBe("orders.topic");
        builder.Exchanges[0].Type.ShouldBe(global::RabbitMQ.Client.ExchangeType.Topic);

        builder.Queues.Count.ShouldBe(1);
        builder.Queues[0].Name.ShouldBe("orders.processing.queue");

        builder.Bindings.Count.ShouldBe(1);
        builder.Bindings[0].RoutingKey.ShouldBe("orders.#");
    }

    [Fact]
    public void DiRegistration_EnterpriseFeatures_RegistersAllServices()
    {
        var key = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(key);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRabbitMQ();
        services.AddKyrolusRabbitMQOutbox(TimeSpan.FromSeconds(1));
        services.AddKyrolusRabbitMQIdempotency();
        services.AddKyrolusRabbitMQTopology(b => b.AddExchange("app.exchange"));
        services.AddKyrolusRabbitMQEncryption(key);
        services.AddKyrolusRabbitMQCompression();

        var provider = services.BuildServiceProvider();

        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Outbox.IKyrolusOutboxStore>().ShouldNotBeNull();
        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Idempotency.IKyrolusIdempotencyStore>().ShouldNotBeNull();
        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Topology.IKyrolusRabbitMQTopologyBuilder>().ShouldNotBeNull();
        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Security.IKyrolusMessageEncryptor>().ShouldNotBeNull();
        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Compression.IKyrolusMessageCompressor>().ShouldNotBeNull();
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
