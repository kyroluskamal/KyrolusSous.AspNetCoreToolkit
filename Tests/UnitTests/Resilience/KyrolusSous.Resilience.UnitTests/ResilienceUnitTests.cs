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

[KyrolusResilient(PipelineName = "default")]
public record TestResilientCommand(string Name) : IKyrolusRequest<string>;

public class ResilienceUnitTests
{
    [Fact]
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

    [Fact]
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
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
    public async Task ResilienceCircuitBreakerHealthCheck_ReturnsHealthy()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddKyrolusResilience();

        var provider = services.BuildServiceProvider();
        var pipelineProvider = provider.GetRequiredService<IKyrolusResiliencePipelineProvider>();
        var healthCheck = new KyrolusResilienceCircuitBreakerHealthCheck(pipelineProvider);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());
        result.Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
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
}
