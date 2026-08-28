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

    [Fact]
    public async Task SagaCoordinator_ExecutesStepsAndCompensatesOnFailure_Correctly()
    {
        var store = new KyrolusSous.RabbitMQ.Runtime.Sagas.KyrolusInMemorySagaStore<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>();
        var coordinator = new KyrolusSous.RabbitMQ.Runtime.Sagas.KyrolusSagaCoordinator<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>(store);

        var state = new KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState
        {
            CorrelationId = "saga-1234"
        };

        bool step1Compensated = false;

        await coordinator.ExecuteStepAsync(
            state,
            "Step1_ReserveStock",
            () => Task.CompletedTask,
            () => { step1Compensated = true; return Task.CompletedTask; });

        state.CurrentState.ShouldBe("Step1_ReserveStock");

        // Step 2 fails and should trigger step 1 compensation
        await Should.ThrowAsync<InvalidOperationException>(async () =>
        {
            await coordinator.ExecuteStepAsync(
                state,
                "Step2_ChargePayment",
                () => throw new InvalidOperationException("Card declined"),
                () => Task.CompletedTask);
        });

        step1Compensated.ShouldBeTrue();
        state.CurrentState.ShouldBe("Compensated");
        state.IsFaulted.ShouldBeTrue();
    }

    [Fact]
    public void CloudEventEnvelope_InitializesWithStandardAttributes_Correctly()
    {
        var payload = new { TransactionId = "tx-99", Amount = 1500.00 };
        var envelope = new KyrolusSous.RabbitMQ.Abstractions.Models.KyrolusCloudEventEnvelope<object>(
            payload,
            source: "/billing-service",
            subject: "tx/created");

        envelope.SpecVersion.ShouldBe("1.0");
        envelope.Source.ShouldBe("/billing-service");
        envelope.Subject.ShouldBe("tx/created");
        envelope.DataContentType.ShouldBe("application/json");
        envelope.Data.ShouldBe(payload);
        envelope.Time.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow);
    }

    [Fact]
    public void MessageUpcaster_TransformsV1ToV2_Successfully()
    {
        var registry = new KyrolusSous.RabbitMQ.Runtime.Evolution.KyrolusMessageUpcasterRegistry();
        registry.Register(new OrderPlacedV1ToV2Upcaster());

        var v1 = new OrderPlacedV1(101, "Alice");
        var upcasted = registry.Upcast(v1);

        upcasted.ShouldBeOfType<OrderPlacedV2>();
        var v2 = (OrderPlacedV2)upcasted;
        v2.OrderId.ShouldBe(101);
        v2.CustomerName.ShouldBe("Alice");
        v2.Email.ShouldBe("unspecified@domain.com");
    }

    [Fact]
    public async Task RateLimiter_TokenBucket_LimitsAndAcquiresTokens()
    {
        var limiter = new KyrolusSous.RabbitMQ.Runtime.RateLimiting.KyrolusTokenBucketRateLimiter(maxTokensPerSecond: 10, burstCapacity: 2);

        var canAcquire1 = limiter.TryAcquire(1);
        canAcquire1.ShouldBeTrue();

        var canAcquire2 = limiter.TryAcquire(1);
        canAcquire2.ShouldBeTrue();

        // Third immediate acquire without refill should fail or wait
        var canAcquire3 = limiter.TryAcquire(1);
        canAcquire3.ShouldBeFalse();

        // Async acquire should succeed after short refill delay
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await limiter.AcquireAsync(1, cts.Token);
    }

    [Fact]
    public void TopologyBuilder_PriorityQuorumStreamsAndHeaders_ConfiguresCorrectly()
    {
        var builder = new KyrolusSous.RabbitMQ.Runtime.Topology.KyrolusRabbitMQTopologyBuilder();
        builder.AddPriorityQueue("vip.queue", maxPriority: 5)
               .AddQuorumQueue("audit.quorum.queue", deliveryLimit: 3)
               .AddStream("telemetry.stream", maxAge: TimeSpan.FromHours(24))
               .BindHeadersQueue("tenant.queue", "headers.exchange", "all", new Dictionary<string, object?> { ["tenant"] = "emea" });

        builder.Queues.Count.ShouldBe(3);
        builder.Queues.ShouldContain(q => q.Name == "vip.queue" && q.Arguments != null && q.Arguments.ContainsKey("x-max-priority"));
        builder.Queues.ShouldContain(q => q.Name == "audit.quorum.queue" && q.Arguments != null && (string)q.Arguments["x-queue-type"]! == "quorum");
        builder.Queues.ShouldContain(q => q.Name == "telemetry.stream" && q.Arguments != null && (string)q.Arguments["x-queue-type"]! == "stream");

        builder.Bindings.Count.ShouldBe(1);
        builder.Bindings[0].QueueName.ShouldBe("tenant.queue");
        builder.Bindings[0].Arguments.ShouldNotBeNull();
        builder.Bindings[0].Arguments!["x-match"].ShouldBe("all");
        builder.Bindings[0].Arguments!["tenant"].ShouldBe("emea");
    }

    [Fact]
    public void DiRegistration_UltraAdvancedFeatures_RegistersAllServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusRabbitMQ();
        services.AddKyrolusRabbitMQSaga<TestOrderSaga, KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>();
        services.AddKyrolusRabbitMQDlqManager();
        services.AddKyrolusRabbitMQUpcasters(r => r.Register(new OrderPlacedV1ToV2Upcaster()));

        var provider = services.BuildServiceProvider();

        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Sagas.IKyrolusSagaStore<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>>().ShouldNotBeNull();
        provider.GetService<TestOrderSaga>().ShouldNotBeNull();
        provider.GetService<KyrolusSous.RabbitMQ.Abstractions.Dlq.IKyrolusDlqManager>().ShouldNotBeNull();
        provider.GetService<KyrolusSous.RabbitMQ.Runtime.Evolution.KyrolusMessageUpcasterRegistry>().ShouldNotBeNull();
    }

    [Fact]
    public void GzipCompressor_DecompressionBomb_ThrowsInvalidOperationException()
    {
        // Limit max decompressed bytes to 500 bytes
        var compressor = new KyrolusSous.RabbitMQ.Runtime.Compression.KyrolusGzipMessageCompressor(maxDecompressedBytes: 500);

        // Generate 2000 bytes
        var payload = new byte[2000];
        Array.Fill(payload, (byte)0x41);

        var compressed = compressor.Compress(payload);

        Should.Throw<InvalidOperationException>(() =>
        {
            compressor.Decompress(compressed);
        }).Message.ShouldContain("Decompression bomb protection");
    }

    [Fact]
    public void UpcasterRegistry_CircularCycle_ThrowsInvalidOperationException()
    {
        var registry = new KyrolusSous.RabbitMQ.Runtime.Evolution.KyrolusMessageUpcasterRegistry();
        registry.Register(new CircularAtoBUpcaster());
        registry.Register(new CircularBtoAUpcaster());

        var objA = new CircularA("Data");

        Should.Throw<InvalidOperationException>(() =>
        {
            registry.Upcast(objA);
        }).Message.ShouldContain("Circular schema upcasting loop detected");
    }

    [Fact]
    public async Task SagaCoordinator_MultipleCompensations_ExecutesInReverseLifoOrder()
    {
        var store = new KyrolusSous.RabbitMQ.Runtime.Sagas.KyrolusInMemorySagaStore<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>();
        var coordinator = new KyrolusSous.RabbitMQ.Runtime.Sagas.KyrolusSagaCoordinator<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>(store);

        var state = new KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState { CorrelationId = "saga-lifo-1" };
        var executedCompensations = new List<string>();

        await coordinator.ExecuteStepAsync(state, "Step1", () => Task.CompletedTask, () => { executedCompensations.Add("Compensate1"); return Task.CompletedTask; });
        await coordinator.ExecuteStepAsync(state, "Step2", () => Task.CompletedTask, () => { executedCompensations.Add("Compensate2"); return Task.CompletedTask; });
        await coordinator.ExecuteStepAsync(state, "Step3", () => Task.CompletedTask, () => { executedCompensations.Add("Compensate3"); return Task.CompletedTask; });

        await coordinator.CompensateAsync(state);

        // Must be in reverse order: Step 3 -> Step 2 -> Step 1
        executedCompensations.ShouldBe(["Compensate3", "Compensate2", "Compensate1"]);
    }

    [Fact]
    public void CircuitBreaker_HalfOpenProbe_AllowsOnlyOneExecutionAtATime()
    {
        var breaker = new KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusConsumerCircuitBreaker(
            consecutiveFailureThreshold: 1,
            breakDuration: TimeSpan.FromMilliseconds(50));

        breaker.ReportFailure();
        breaker.CanExecute().ShouldBeFalse();

        // Wait for break duration to transition to HalfOpen
        Thread.Sleep(70);

        breaker.State.ShouldBe(KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusCircuitState.HalfOpen);

        // First execution probe should be permitted
        var probe1 = breaker.CanExecute();
        probe1.ShouldBeTrue();

        // Second concurrent execution before probe finishes must be rejected
        var probe2 = breaker.CanExecute();
        probe2.ShouldBeFalse();

        // After success, circuit closes
        breaker.ReportSuccess();
        breaker.State.ShouldBe(KyrolusSous.RabbitMQ.Runtime.Resilience.KyrolusCircuitState.Closed);
    }

    [Fact]
    public void TopologyBuilder_DeduplicatesExchangesAndQueues_Properly()
    {
        var builder = new KyrolusSous.RabbitMQ.Runtime.Topology.KyrolusRabbitMQTopologyBuilder();
        builder.AddExchange("events.exchange")
               .AddExchange("events.exchange") // duplicate
               .AddQueue("events.queue")
               .AddQueue("events.queue") // duplicate
               .BindQueue("events.queue", "events.exchange", "order.created")
               .BindQueue("events.queue", "events.exchange", "order.created"); // duplicate

        builder.Exchanges.Count.ShouldBe(1);
        builder.Queues.Count.ShouldBe(1);
        builder.Bindings.Count.ShouldBe(1);
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

public sealed record OrderPlacedV1(int OrderId, string CustomerName);
public sealed record OrderPlacedV2(int OrderId, string CustomerName, string Email);

public sealed class OrderPlacedV1ToV2Upcaster : KyrolusSous.RabbitMQ.Abstractions.Evolution.IKyrolusMessageUpcaster<OrderPlacedV1, OrderPlacedV2>
{
    public OrderPlacedV2 Upcast(OrderPlacedV1 oldMessage)
    {
        return new OrderPlacedV2(oldMessage.OrderId, oldMessage.CustomerName, "unspecified@domain.com");
    }
}

public sealed class TestOrderSaga : KyrolusSous.RabbitMQ.Runtime.Sagas.KyrolusSagaCoordinator<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState>
{
    public TestOrderSaga(KyrolusSous.RabbitMQ.Abstractions.Sagas.IKyrolusSagaStore<KyrolusSous.RabbitMQ.Abstractions.Sagas.KyrolusSagaState> store)
        : base(store)
    {
    }
}

public sealed record CircularA(string Value);
public sealed record CircularB(string Value);

public sealed class CircularAtoBUpcaster : KyrolusSous.RabbitMQ.Abstractions.Evolution.IKyrolusMessageUpcaster<CircularA, CircularB>
{
    public CircularB Upcast(CircularA oldMessage) => new(oldMessage.Value);
}

public sealed class CircularBtoAUpcaster : KyrolusSous.RabbitMQ.Abstractions.Evolution.IKyrolusMessageUpcaster<CircularB, CircularA>
{
    public CircularA Upcast(CircularB oldMessage) => new(oldMessage.Value);
}
