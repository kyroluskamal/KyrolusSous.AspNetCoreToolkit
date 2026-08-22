using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Threading.RateLimiting;

namespace KyrolusSous.Resilience;

public class KyrolusResiliencePipelineProvider : IKyrolusResiliencePipelineProvider, IDisposable
{
    private static readonly ActivitySource ActivitySource = new("KyrolusSous.Resilience", "1.0.0");

    private readonly IOptionsMonitor<KyrolusResilienceOptions>? _optionsMonitor;
    private readonly KyrolusResilienceOptions _staticOptions;
    private readonly ILogger<KyrolusResiliencePipelineProvider>? _logger;
    private readonly IReadOnlyDictionary<string, IKyrolusCustomPipelineConfigurator> _customConfigurators;
    private readonly ConcurrentDictionary<string, ResiliencePipeline> _pipelines = new(StringComparer.OrdinalIgnoreCase);
    private readonly IDisposable? _changeSubscription;

    public KyrolusResiliencePipelineProvider(
        IOptionsMonitor<KyrolusResilienceOptions>? optionsMonitor = null,
        IOptions<KyrolusResilienceOptions>? options = null,
        IEnumerable<IKyrolusCustomPipelineConfigurator>? customConfigurators = null,
        ILogger<KyrolusResiliencePipelineProvider>? logger = null)
    {
        _optionsMonitor = optionsMonitor;
        _staticOptions = options?.Value ?? new KyrolusResilienceOptions();
        _logger = logger;

        _customConfigurators = customConfigurators?
            .ToDictionary(c => c.Name, c => c, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, IKyrolusCustomPipelineConfigurator>(StringComparer.OrdinalIgnoreCase);

        if (_optionsMonitor is not null)
        {
            _changeSubscription = _optionsMonitor.OnChange(OnOptionsChanged);
        }
    }

    private KyrolusResilienceOptions CurrentOptions => _optionsMonitor?.CurrentValue ?? _staticOptions;

    public ResiliencePipeline GetPipeline(string name = "default")
    {
        return _pipelines.GetOrAdd(name, CreatePipeline);
    }

    public ResiliencePipeline<TResult> GetPipeline<TResult>(string name = "default")
    {
        var options = CurrentOptions;
        var builder = new ResiliencePipelineBuilder<TResult>();

        builder.AddRetry(new RetryStrategyOptions<TResult>
        {
            ShouldHandle = new PredicateBuilder<TResult>()
                .Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
            MaxRetryAttempts = options.Retry.MaxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(options.Retry.InitialDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = options.Retry.UseJitter,
            OnRetry = args =>
            {
                using var activity = ActivitySource.StartActivity("Resilience.Retry");
                activity?.SetTag("resilience.pipeline", name);
                activity?.SetTag("resilience.attempt", args.AttemptNumber);

                _logger?.LogWarning("Resilience retry on '{Pipeline}' attempt {Attempt}. Error: {Error}",
                    name, args.AttemptNumber, args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions<TResult>
        {
            ShouldHandle = new PredicateBuilder<TResult>()
                .Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
            FailureRatio = options.CircuitBreaker.FailureRatio,
            MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreaker.SamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.CircuitBreaker.BreakDurationSeconds),
            OnOpened = args =>
            {
                using var activity = ActivitySource.StartActivity("Resilience.CircuitBreaker.Opened");
                activity?.SetTag("resilience.pipeline", name);
                activity?.SetTag("resilience.break_duration", args.BreakDuration.TotalSeconds);

                _logger?.LogError("Circuit breaker opened for '{Pipeline}'. Breaking for {BreakDuration}s.",
                    name, args.BreakDuration.TotalSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                _logger?.LogInformation("Circuit breaker closed for '{Pipeline}'. Normal operation resumed.", name);
                return ValueTask.CompletedTask;
            }
        });

        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(options.Timeout.TotalTimeoutSeconds)
        });

        return builder.Build();
    }

    private ResiliencePipeline CreatePipeline(string name)
    {
        if (_customConfigurators.TryGetValue(name, out var customConfigurator))
        {
            var customBuilder = new ResiliencePipelineBuilder();
            customConfigurator.Configure(customBuilder);
            return customBuilder.Build();
        }

        return name.ToLowerInvariant() switch
        {
            "database" => CreateDatabasePipeline(),
            "external_service" => CreateExternalServicePipeline(),
            "messaging" => CreateMessagingPipeline(),
            "bulkhead" => CreateBulkheadPipeline(),
            _ => CreateDefaultPipeline(name)
        };
    }

    private ResiliencePipeline CreateDefaultPipeline(string name)
    {
        var options = CurrentOptions;
        var builder = new ResiliencePipelineBuilder();

        builder.AddRetry(new RetryStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
            MaxRetryAttempts = options.Retry.MaxRetryAttempts,
            Delay = TimeSpan.FromMilliseconds(options.Retry.InitialDelayMs),
            BackoffType = DelayBackoffType.Exponential,
            UseJitter = options.Retry.UseJitter,
            OnRetry = args =>
            {
                using var activity = ActivitySource.StartActivity("Resilience.Retry");
                activity?.SetTag("resilience.pipeline", name);
                activity?.SetTag("resilience.attempt", args.AttemptNumber);

                _logger?.LogWarning("Resilience retry on '{Pipeline}' attempt {Attempt}. Error: {Error}",
                    name, args.AttemptNumber, args.Outcome.Exception?.Message);
                return ValueTask.CompletedTask;
            }
        });

        builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
        {
            ShouldHandle = new PredicateBuilder().Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
            FailureRatio = options.CircuitBreaker.FailureRatio,
            MinimumThroughput = options.CircuitBreaker.MinimumThroughput,
            SamplingDuration = TimeSpan.FromSeconds(options.CircuitBreaker.SamplingDurationSeconds),
            BreakDuration = TimeSpan.FromSeconds(options.CircuitBreaker.BreakDurationSeconds),
            OnOpened = args =>
            {
                using var activity = ActivitySource.StartActivity("Resilience.CircuitBreaker.Opened");
                activity?.SetTag("resilience.pipeline", name);

                _logger?.LogError("Circuit breaker opened for '{Pipeline}'. Breaking for {BreakDuration}s.",
                    name, args.BreakDuration.TotalSeconds);
                return ValueTask.CompletedTask;
            },
            OnClosed = _ =>
            {
                _logger?.LogInformation("Circuit breaker closed for '{Pipeline}'. Normal operation resumed.", name);
                return ValueTask.CompletedTask;
            }
        });

        builder.AddTimeout(new TimeoutStrategyOptions
        {
            Timeout = TimeSpan.FromSeconds(options.Timeout.TotalTimeoutSeconds)
        });

        return builder.Build();
    }

    private ResiliencePipeline CreateDatabasePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(50),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(15)
            })
            .Build();
    }

    private ResiliencePipeline CreateExternalServicePipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(500),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
                FailureRatio = 0.5,
                MinimumThroughput = 5,
                SamplingDuration = TimeSpan.FromSeconds(10),
                BreakDuration = TimeSpan.FromSeconds(30)
            })
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            })
            .Build();
    }

    private ResiliencePipeline CreateMessagingPipeline()
    {
        return new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder().Handle<Exception>(KyrolusTransientEvaluator.IsTransient),
                MaxRetryAttempts = 5,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();
    }

    private ResiliencePipeline CreateBulkheadPipeline()
    {
        var options = CurrentOptions;
        return new ResiliencePipelineBuilder()
            .AddConcurrencyLimiter(new ConcurrencyLimiterOptions
            {
                PermitLimit = options.RateLimiter.PermitLimit,
                QueueLimit = options.RateLimiter.QueueLimit
            })
            .Build();
    }

    private void OnOptionsChanged(KyrolusResilienceOptions newOptions)
    {
        _logger?.LogInformation("Resilience configuration changed in real-time. Invalidating pipeline cache for zero-downtime reload.");
        _pipelines.Clear();
    }

    public void Dispose()
    {
        _changeSubscription?.Dispose();
        GC.SuppressFinalize(this);
    }
}
