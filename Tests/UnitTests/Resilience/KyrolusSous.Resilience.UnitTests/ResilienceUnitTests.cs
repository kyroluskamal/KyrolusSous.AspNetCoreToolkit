using System.Net;
using System.Net.Sockets;
using KyrolusSous.ExceptionHandling.Abstractions.Exceptions;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using Shouldly;
using Xunit;

namespace KyrolusSous.Resilience.UnitTests;

public class CustomTransientDomainException : KyrolusException
{
    public CustomTransientDomainException(string message)
        : base(HttpStatusCode.ServiceUnavailable, "service_unavailable", "Service unavailable", message, isTransient: true)
    {
    }
}

public class CustomPermanentDomainException : KyrolusException
{
    public CustomPermanentDomainException(string message)
        : base(HttpStatusCode.BadRequest, "bad_request", "Bad request", message, isTransient: false)
    {
    }
}

public class CustomPostgreSqlLockException(string message) : Exception(message);

public class CustomPostgresTransientEvaluator : IKyrolusTransientExceptionEvaluator
{
    public bool IsTransient(Exception exception) => exception is CustomPostgreSqlLockException;
}

public interface ITestGreetingService
{
    Task<string> GreetAsync(string name);
}

public class TestGreetingService : ITestGreetingService
{
    public int Calls = 0;
    public async Task<string> GreetAsync(string name)
    {
        await Task.Yield();
        Calls++;
        if (Calls < 3)
        {
            throw new CustomTransientDomainException("Service busy");
        }
        return $"Hello, {name}!";
    }
}

public class MockFailingHttpMessageHandler : HttpMessageHandler
{
    public int Calls = 0;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        if (Calls < 3)
        {
            throw new HttpRequestException("Network failure", null, HttpStatusCode.ServiceUnavailable);
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
    }
}

public class MockResilienceAlertSink : IKyrolusResilienceAlertSink
{
    public readonly List<KyrolusResilienceAlert> Alerts = [];
    public Task PublishAlertAsync(KyrolusResilienceAlert alert, CancellationToken cancellationToken = default)
    {
        Alerts.Add(alert);
        return Task.CompletedTask;
    }
}

public class MockAlertHandler : IKyrolusResilienceAlertHandler
{
    public readonly List<KyrolusResilienceAlert> Handled = [];
    public ValueTask HandleAlertAsync(KyrolusResilienceAlert alert, CancellationToken cancellationToken = default)
    {
        Handled.Add(alert);
        return ValueTask.CompletedTask;
    }
}

[KyrolusResilient(PipelineName = "default")]
public record TestResilientCommand(string Name) : IKyrolusRequest<string>;

public class ResilienceUnitTests
{
    [Fact(DisplayName = "Kyrolus Transient Evaluator Evaluates Transient Exceptions Correctly")]
    public void KyrolusTransientEvaluator_EvaluatesTransientExceptionsCorrectly()
    {
        KyrolusTransientEvaluator.IsTransient(new CustomTransientDomainException("temporary issue")).ShouldBeTrue();
        KyrolusTransientEvaluator.IsTransient(new CustomPermanentDomainException("permanent issue")).ShouldBeFalse();
        KyrolusTransientEvaluator.IsTransient(new TimeoutException()).ShouldBeTrue();
        KyrolusTransientEvaluator.IsTransient(new SocketException()).ShouldBeTrue();
        KyrolusTransientEvaluator.IsTransient(new HttpRequestException("Gateway error", null, HttpStatusCode.BadGateway)).ShouldBeTrue();
        KyrolusTransientEvaluator.IsTransient(new HttpRequestException("Rate limit", null, HttpStatusCode.TooManyRequests)).ShouldBeTrue();
        KyrolusTransientEvaluator.IsTransient(new HttpRequestException("Not found", null, HttpStatusCode.NotFound)).ShouldBeFalse();
        KyrolusTransientEvaluator.IsTransient(new InvalidOperationException("User error")).ShouldBeFalse();
    }

    [Fact(DisplayName = "Kyrolus Resilience Options Defaults Are Valid")]
    public void KyrolusResilienceOptions_Defaults_AreValid()
    {
        var options = new KyrolusResilienceOptions();

        options.Retry.MaxRetryAttempts.ShouldBe(3);
        options.Retry.InitialDelayMs.ShouldBe(200);
        options.Retry.UseJitter.ShouldBeTrue();

        options.CircuitBreaker.FailureRatio.ShouldBe(0.5);
        options.CircuitBreaker.SamplingDurationSeconds.ShouldBe(10);
        options.CircuitBreaker.BreakDurationSeconds.ShouldBe(30);

        options.Timeout.TotalTimeoutSeconds.ShouldBe(30);
        options.Hedging.Enabled.ShouldBeFalse();
    }

    [Fact(DisplayName = "Add Kyrolus Resilience Binds Configuration Correctly")]
    public void AddKyrolusResilience_BindsConfigurationCorrectly()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "KyrolusResilience:Retry:MaxRetryAttempts", "5" },
            { "KyrolusResilience:Retry:InitialDelayMs", "100" },
            { "KyrolusResilience:CircuitBreaker:FailureRatio", "0.7" },
            { "KyrolusResilience:Timeout:TotalTimeoutSeconds", "45" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(configuration.GetSection("KyrolusResilience"));

        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<KyrolusResilienceOptions>>().Value;

        options.Retry.MaxRetryAttempts.ShouldBe(5);
        options.Retry.InitialDelayMs.ShouldBe(100);
        options.CircuitBreaker.FailureRatio.ShouldBe(0.7);
        options.Timeout.TotalTimeoutSeconds.ShouldBe(45);

        var pipelineProvider = provider.GetService<IKyrolusResiliencePipelineProvider>();
        pipelineProvider.ShouldNotBeNull();
    }

    [Fact(DisplayName = "Resilience Pipeline Retries On Transient Exception And Succeeds")]
    public async Task ResiliencePipeline_RetriesOnTransientException_AndSucceeds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.InitialDelayMs = 10;
        });

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();

        var attempts = 0;
        var result = await pipelineProvider.ExecuteWithResilienceAsync(async ct =>
        {
            await Task.Yield();
            attempts++;
            if (attempts < 3)
            {
                throw new CustomTransientDomainException("Temporary glitch");
            }
            return "success";
        });

        result.ShouldBe("success");
        attempts.ShouldBe(3);
    }

    [Fact(DisplayName = "Resilience Pipeline Does Not Retry On Permanent Exception")]
    public async Task ResiliencePipeline_DoesNotRetryOnPermanentException()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.InitialDelayMs = 10;
        });

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();

        var attempts = 0;
        await Should.ThrowAsync<CustomPermanentDomainException>(async () =>
        {
            await pipelineProvider.ExecuteWithResilienceAsync(async ct =>
            {
                await Task.Yield();
                attempts++;
                throw new CustomPermanentDomainException("Permanent bad request");
            });
        });

        attempts.ShouldBe(1);
    }

    [Fact(DisplayName = "Execute With Fallback Async Executes Fallback On Failure")]
    public async Task ExecuteWithFallbackAsync_ExecutesFallbackOnFailure()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.InitialDelayMs = 5;
        });

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();

        var result = await pipelineProvider.ExecuteWithFallbackAsync(
            action: async ct =>
            {
                await Task.Yield();
                throw new CustomTransientDomainException("Service down");
#pragma warning disable CS0162
                return "live_value";
#pragma warning restore CS0162
            },
            fallback: async (ex, ct) =>
            {
                await Task.Yield();
                return "cached_fallback_value";
            });

        result.ShouldBe("cached_fallback_value");
    }

    [Fact(DisplayName = "Resilience Pipeline Behavior Intercepts Mediator Request And Retries")]
    public async Task ResiliencePipelineBehavior_InterceptsMediatorRequest_AndRetries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.InitialDelayMs = 5;
        });

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();
        var behavior = new KyrolusResiliencePipelineBehavior<TestResilientCommand, string>(pipelineProvider);

        var attempts = 0;
        var command = new TestResilientCommand("CreateOrder");

        var response = await behavior.Handle(command, (ct) =>
        {
            attempts++;
            if (attempts < 2)
            {
                throw new CustomTransientDomainException("Concurrency deadlock");
            }
            return Task.FromResult("OrderCreated");
        }, CancellationToken.None);

        response.ShouldBe("OrderCreated");
        attempts.ShouldBe(2);
    }

    [Fact(DisplayName = "Resilience Circuit Breaker Health Check Returns Healthy")]
    public async Task ResilienceCircuitBreakerHealthCheck_ReturnsHealthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience();

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();
        var observer = provider.GetRequiredService<IKyrolusCircuitBreakerObserver>();
        var healthCheck = new KyrolusResilienceCircuitBreakerHealthCheck(pipelineProvider, observer);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact(DisplayName = "Add Kyrolus Custom Resilience Pipeline Registers Custom Named Pipeline")]
    public async Task AddKyrolusCustomResiliencePipeline_RegistersCustomNamedPipeline()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience();
        services.AddKyrolusCustomResiliencePipeline("custom_payment", builder =>
        {
            builder.AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 4,
                Delay = TimeSpan.FromMilliseconds(5)
            });
        });

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();

        var attempts = 0;
        var result = await pipelineProvider.ExecuteWithResilienceAsync(async ct =>
        {
            await Task.Yield();
            attempts++;
            if (attempts < 4)
            {
                throw new CustomTransientDomainException("Payment gateway slow");
            }
            return "PaymentAuthorized";
        }, pipelineName: "custom_payment");

        result.ShouldBe("PaymentAuthorized");
        attempts.ShouldBe(4);
    }

    [Fact(DisplayName = "Custom Transient Evaluator Can Be Registered And Extends Retry Behavior")]
    public async Task CustomTransientEvaluator_CanBeRegisteredAndExtendsRetryBehavior()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.InitialDelayMs = 5;
        });
        services.AddTransientExceptionEvaluator<CustomPostgresTransientEvaluator>();

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();

        var attempts = 0;
        var result = await pipelineProvider.ExecuteWithResilienceAsync(async ct =>
        {
            await Task.Yield();
            attempts++;
            if (attempts < 2)
            {
                throw new CustomPostgreSqlLockException("Postgres deadlock error");
            }
            return "recovered";
        });

        result.ShouldBe("recovered");
        attempts.ShouldBe(2);
    }

    [Fact(DisplayName = "Circuit Breaker Observer Tracks State Transitions And Manual Controls")]
    public void CircuitBreakerObserver_TracksStateTransitions_AndManualControls()
    {
        var observer = new KyrolusCircuitBreakerObserver();
        observer.GetCircuitState("database").ShouldBe(KyrolusCircuitState.Closed);

        observer.ForceOpen("database");
        observer.GetCircuitState("database").ShouldBe(KyrolusCircuitState.Open);

        var info = observer.GetCircuitInfo("database");
        info.State.ShouldBe(KyrolusCircuitState.Open);

        observer.ForceClose("database");
        observer.GetCircuitState("database").ShouldBe(KyrolusCircuitState.Closed);

        observer.Reset("database");
        observer.GetCircuitState("database").ShouldBe(KyrolusCircuitState.Closed);
    }

    [Fact(DisplayName = "Adaptive Concurrency Limiter Adapts Limit On Success And Failure")]
    public void AdaptiveConcurrencyLimiter_AdaptsLimitOnSuccessAndFailure()
    {
        var limiter = new KyrolusAdaptiveConcurrencyLimiter(initialLimit: 10, minLimit: 2, maxLimit: 50);
        limiter.CurrentLimit.ShouldBe(10);

        limiter.TryAcquire().ShouldBeTrue();
        limiter.InFlightRequests.ShouldBe(1);

        // Fast execution duration -> grows limit
        limiter.Release(TimeSpan.FromMilliseconds(5), success: true);
        limiter.InFlightRequests.ShouldBe(0);

        // Failure -> backs off limit
        limiter.TryAcquire().ShouldBeTrue();
        limiter.Release(TimeSpan.FromMilliseconds(100), success: false);
        limiter.CurrentLimit.ShouldBeLessThan(10);
    }

    [Fact(DisplayName = "Add Resilient Decorated Wraps Interface With Resilience Proxy")]
    public async Task AddResilientDecorated_WrapsInterfaceWithResilienceProxy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 4;
            options.Retry.InitialDelayMs = 5;
        });

        services.AddResilientDecorated<ITestGreetingService, TestGreetingService>("default");

        var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<ITestGreetingService>();

        var result = await service.GreetAsync("Kyrolus");
        result.ShouldBe("Hello, Kyrolus!");
    }

    [Fact(DisplayName = "Fallback Registry Resolves And Executes Declarative Fallback")]
    public async Task FallbackRegistry_ResolvesAndExecutesDeclarativeFallback()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.InitialDelayMs = 5;
        });

        services.AddResilienceFallback<string>("payment_fallback_pipe", (ex, ct) => ValueTask.FromResult("fallback_payment_response"));

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();
        var pipeline = pipelineProvider.GetPipeline<string>("payment_fallback_pipe");

        var result = await pipeline.ExecuteAsync(async ct =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Payment downstream completely offline");
#pragma warning disable CS0162
            return "real_payment_response";
#pragma warning restore CS0162
        });

        result.ShouldBe("fallback_payment_response");
    }

    [Fact(DisplayName = "SingleFlight Coalesces Concurrent Requests And Executes Factory Once")]
    public async Task SingleFlight_CoalescesConcurrentRequests_ExecutesFactoryOnce()
    {
        var singleFlight = new KyrolusSingleFlight();
        var executionCount = 0;

        var tasks = Enumerable.Range(0, 10).Select(_ => singleFlight.DoAsync("user_profile_123", async ct =>
        {
            await Task.Delay(20, ct);
            Interlocked.Increment(ref executionCount);
            return "user_data_payload";
        })).ToList();

        var results = await Task.WhenAll(tasks);

        executionCount.ShouldBe(1);
        results.All(r => r == "user_data_payload").ShouldBeTrue();
    }

    [Fact(DisplayName = "Partitioned Rate Limiter Isolates Limits Per Partition Key")]
    public void PartitionedRateLimiter_IsolatesLimitsPerPartitionKey()
    {
        var options = Options.Create(new KyrolusResilienceOptions
        {
            PartitionedRateLimiter = new KyrolusPartitionedRateLimiterOptionsConfig
            {
                Enabled = true,
                PermitsPerPartition = 2
            }
        });

        var limiter = new KyrolusPartitionedRateLimiter(options: options);

        // Tenant A acquires 2 permits
        limiter.TryAcquire("tenant_A").ShouldBeTrue();
        limiter.TryAcquire("tenant_A").ShouldBeTrue();
        limiter.TryAcquire("tenant_A").ShouldBeFalse(); // Throttled

        // Tenant B is unaffected
        limiter.TryAcquire("tenant_B").ShouldBeTrue();

        // Release Tenant A permit
        limiter.Release("tenant_A");
        limiter.TryAcquire("tenant_A").ShouldBeTrue();
    }

    [Fact(DisplayName = "Chaos Engine Injects Latency And Exceptions When Configured")]
    public async Task ChaosEngine_InjectsLatencyAndExceptionsWhenConfigured()
    {
        var options = Options.Create(new KyrolusResilienceOptions
        {
            Chaos = new KyrolusChaosOptionsConfig
            {
                Enabled = true,
                InjectionRate = 1.0, // 100% injection
                InjectedLatencyMs = 10,
                InjectTransientErrors = true
            }
        });

        var chaos = new KyrolusChaosEngine(options: options);

        await Should.ThrowAsync<HttpRequestException>(async () =>
        {
            await chaos.MaybeInjectFaultAsync("test_pipeline");
        });
    }

    [Fact(DisplayName = "HttpClient Resilience Delegating Handler Retries Failed Http Requests")]
    public async Task HttpClient_ResilienceDelegatingHandler_RetriesFailedHttpRequests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.Retry.InitialDelayMs = 5;
        });

        var mockHandler = new MockFailingHttpMessageHandler();
        services.AddHttpClient("TestApiClient")
            .AddKyrolusResilienceHandler("default")
            .ConfigurePrimaryHttpMessageHandler(() => mockHandler);

        var provider = services.BuildServiceProvider();
        var clientFactory = provider.GetRequiredService<IHttpClientFactory>();
        var client = clientFactory.CreateClient("TestApiClient");

        var response = await client.GetAsync("http://example.com/api/test");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        mockHandler.Calls.ShouldBe(3);
    }

    [Fact(DisplayName = "Priority Load Shedder Sheds Low Priority Requests Under High Cpu Load")]
    public void PriorityLoadShedder_ShedsLowPriorityRequestsUnderHighCpuLoad()
    {
        var shedder = new KyrolusPriorityLoadShedder();

        // Under 50% CPU: nothing is shed
        shedder.ReportCpuLoad(50.0);
        shedder.ShouldShed(KyrolusRequestPriority.Critical).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.High).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.Normal).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.Background).ShouldBeFalse();

        // Under 80% CPU: Low/Background shed
        shedder.ReportCpuLoad(80.0);
        shedder.ShouldShed(KyrolusRequestPriority.Critical).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.Normal).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.Background).ShouldBeTrue();

        // Under 90% CPU: Normal shed, Critical/High preserved
        shedder.ReportCpuLoad(90.0);
        shedder.ShouldShed(KyrolusRequestPriority.Critical).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.High).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.Normal).ShouldBeTrue();

        // Under 96% CPU: High shed, Critical preserved
        shedder.ReportCpuLoad(96.0);
        shedder.ShouldShed(KyrolusRequestPriority.Critical).ShouldBeFalse();
        shedder.ShouldShed(KyrolusRequestPriority.High).ShouldBeTrue();
    }

    [Fact(DisplayName = "Adaptive Timeout Estimator Calculates Dynamic Thresholds")]
    public void AdaptiveTimeoutEstimator_CalculatesDynamicThresholds()
    {
        var estimator = new KyrolusAdaptiveTimeoutEstimator
        {
            MinTimeout = TimeSpan.FromMilliseconds(50),
            MaxTimeout = TimeSpan.FromSeconds(5)
        };

        // Record fast samples
        for (var i = 0; i < 20; i++)
        {
            estimator.RecordDuration("fast_service", TimeSpan.FromMilliseconds(100));
        }

        var timeout = estimator.GetDynamicTimeout("fast_service");
        timeout.TotalMilliseconds.ShouldBeLessThan(1000);
        timeout.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(50);
    }

    [Fact(DisplayName = "Resilience Quarantine Quarantines Repeated Poison Pill Key")]
    public void ResilienceQuarantine_QuarantinesRepeatedPoisonPillKey()
    {
        var quarantine = new KyrolusResilienceQuarantine();
        var key = "poison_payload_abc";

        quarantine.IsQuarantined(key).ShouldBeFalse();

        // Record 2 failures (threshold = 3)
        quarantine.RecordFailure(key, failureThreshold: 3);
        quarantine.RecordFailure(key, failureThreshold: 3);
        quarantine.IsQuarantined(key).ShouldBeFalse();

        // 3rd failure trips quarantine
        quarantine.RecordFailure(key, failureThreshold: 3, quarantineDuration: TimeSpan.FromSeconds(5));
        quarantine.IsQuarantined(key).ShouldBeTrue();

        // Success clears quarantine
        quarantine.RecordSuccess(key);
        quarantine.IsQuarantined(key).ShouldBeFalse();
    }

    [Fact(DisplayName = "Pipeline Composer Chains Multiple Pipelines Sequentially")]
    public async Task PipelineComposer_ChainsMultiplePipelinesSequentially()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.InitialDelayMs = 5;
        });

        var provider = services.BuildServiceProvider();
        var composer = provider.GetRequiredService<IKyrolusResiliencePipelineComposer>();

        var compositePipeline = composer.Compose<string>("default");

        var result = await compositePipeline.ExecuteAsync(async ct =>
        {
            await Task.Yield();
            return "composition_success";
        });

        result.ShouldBe("composition_success");
    }

    [Fact(DisplayName = "Resilience Alert Sink Publishes Alert On Circuit State Change")]
    public void ResilienceAlertSink_PublishesAlertOnCircuitStateChange()
    {
        var alertSink = new MockResilienceAlertSink();
        var observer = new KyrolusCircuitBreakerObserver(alertSink: alertSink);

        observer.ForceOpen("payment_service");

        alertSink.Alerts.Count.ShouldBe(1);
        alertSink.Alerts[0].PipelineName.ShouldBe("payment_service");
        alertSink.Alerts[0].NewState.ShouldBe(KyrolusCircuitState.Open);
    }

    [Fact(DisplayName = "Add Resilience Alert Handler Registers Handler And Receives Alerts")]
    public async Task AddResilienceAlertHandler_RegistersHandlerAndReceivesAlerts()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience();
        var handler = new MockAlertHandler();
        services.AddSingleton<IKyrolusResilienceAlertHandler>(handler);

        var provider = services.BuildServiceProvider();
        var sink = provider.GetRequiredService<IKyrolusResilienceAlertSink>();

        var alert = new KyrolusResilienceAlert("order_pipe", KyrolusCircuitState.Open, "Tripped open", DateTimeOffset.UtcNow);
        await sink.PublishAlertAsync(alert);

        handler.Handled.Count.ShouldBe(1);
        handler.Handled[0].PipelineName.ShouldBe("order_pipe");
    }
}
