using System.Collections.Concurrent;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Resilience;

public sealed class KyrolusMartenNoopResiliencePolicy : IKyrolusMartenResiliencePolicy
{
    public static readonly IKyrolusMartenResiliencePolicy Instance = new KyrolusMartenNoopResiliencePolicy();

    public Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
        => action();

    public Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
        => action();
}

public sealed class KyrolusMartenDelegateResiliencePolicy(
    Func<string, Func<Task>, CancellationToken, Task>? execute = null,
    Func<string, Func<Task<object?>>, CancellationToken, Task<object?>>? executeT = null) : IKyrolusMartenResiliencePolicy
{
    private readonly Func<string, Func<Task>, CancellationToken, Task> exec = execute ?? ((_, action, _) => action());
    private readonly Func<string, Func<Task<object?>>, CancellationToken, Task<object?>> execT = executeT ?? ((_, action, _) => action());

    public async Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        var result = await execT(operation, async () => await action().ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        return (T)result!;
    }

    public Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
        => exec(operation, action, cancellationToken);
}

public sealed class KyrolusMartenRetryResiliencePolicy : IKyrolusMartenResiliencePolicy
{
    private readonly int maxRetries;
    private readonly Func<int, TimeSpan> delayFactory;
    private readonly Func<Exception, bool> shouldRetry;
    private readonly Func<string, bool>? operationFilter;

    public KyrolusMartenRetryResiliencePolicy(
        int maxRetries,
        TimeSpan delay,
        Func<Exception, bool>? shouldRetry = null,
        Func<string, bool>? operationFilter = null)
        : this(maxRetries, _ => delay, shouldRetry, operationFilter)
    {
    }

    public KyrolusMartenRetryResiliencePolicy(
        int maxRetries,
        Func<int, TimeSpan> delayFactory,
        Func<Exception, bool>? shouldRetry = null,
        Func<string, bool>? operationFilter = null)
    {
        if (maxRetries < 0) throw new ArgumentOutOfRangeException(nameof(maxRetries));
        this.maxRetries = maxRetries;
        this.delayFactory = delayFactory ?? throw new ArgumentNullException(nameof(delayFactory));
        this.shouldRetry = shouldRetry ?? (_ => true);
        this.operationFilter = operationFilter;
    }

    public async Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (operationFilter is not null && !operationFilter(operation))
        {
            return await action().ConfigureAwait(false);
        }

        var attempt = 0;
        while (true)
        {
            try
            {
                return await action().ConfigureAwait(false);
            }
            catch (Exception ex) when (attempt < maxRetries && shouldRetry(ex))
            {
                attempt++;
                var delay = delayFactory(attempt);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }

    public async Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (operationFilter is not null && !operationFilter(operation))
        {
            await action().ConfigureAwait(false);
            return;
        }

        var attempt = 0;
        while (true)
        {
            try
            {
                await action().ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (attempt < maxRetries && shouldRetry(ex))
            {
                attempt++;
                var delay = delayFactory(attempt);
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}

public sealed class KyrolusMartenTimeoutResiliencePolicy : IKyrolusMartenResiliencePolicy
{
    private readonly TimeSpan timeout;
    private readonly Func<string, bool>? operationFilter;

    public KyrolusMartenTimeoutResiliencePolicy(TimeSpan timeout, Func<string, bool>? operationFilter = null)
    {
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
        this.timeout = timeout;
        this.operationFilter = operationFilter;
    }

    public async Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (operationFilter is not null && !operationFilter(operation))
        {
            return await action().ConfigureAwait(false);
        }

        var task = action();
        var completed = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        if (completed != task) throw new TimeoutException($"Operation '{operation}' timed out after {timeout}.");
        return await task.ConfigureAwait(false);
    }

    public async Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (operationFilter is not null && !operationFilter(operation))
        {
            await action().ConfigureAwait(false);
            return;
        }

        var task = action();
        var completed = await Task.WhenAny(task, Task.Delay(timeout, cancellationToken)).ConfigureAwait(false);
        if (completed != task) throw new TimeoutException($"Operation '{operation}' timed out after {timeout}.");
        await task.ConfigureAwait(false);
    }
}

public sealed class KyrolusMartenCircuitBreakerResiliencePolicy : IKyrolusMartenResiliencePolicy
{
    private readonly int failureThreshold;
    private readonly TimeSpan breakDuration;
    private readonly Func<Exception, bool> shouldTrip;
    private readonly Func<string, bool>? operationFilter;
    private readonly ConcurrentQueue<DateTimeOffset> failures = new();
    private DateTimeOffset? openUntil;

    public KyrolusMartenCircuitBreakerResiliencePolicy(
        int failureThreshold,
        TimeSpan breakDuration,
        Func<Exception, bool>? shouldTrip = null,
        Func<string, bool>? operationFilter = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(failureThreshold);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(breakDuration, TimeSpan.Zero);
        this.failureThreshold = failureThreshold;
        this.breakDuration = breakDuration;
        this.shouldTrip = shouldTrip ?? (_ => true);
        this.operationFilter = operationFilter;
    }

    public async Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        if (operationFilter is not null && !operationFilter(operation))
        {
            return await action().ConfigureAwait(false);
        }

        if (IsOpen()) throw new InvalidOperationException($"Circuit open for '{operation}'.");

        try
        {
            return await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (shouldTrip(ex))
        {
            RegisterFailure();
            throw;
        }
    }

    public async Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
    {
        if (operationFilter is not null && !operationFilter(operation))
        {
            await action().ConfigureAwait(false);
            return;
        }

        if (IsOpen()) throw new InvalidOperationException($"Circuit open for '{operation}'.");

        try
        {
            await action().ConfigureAwait(false);
        }
        catch (Exception ex) when (shouldTrip(ex))
        {
            RegisterFailure();
            throw;
        }
    }

    private bool IsOpen()
    {
        var now = DateTimeOffset.UtcNow;
        if (openUntil is null) return false;
        if (openUntil <= now)
        {
            openUntil = null;
            return false;
        }
        return true;
    }

    private void RegisterFailure()
    {
        var now = DateTimeOffset.UtcNow;
        failures.Enqueue(now);
        while (failures.TryPeek(out var t) && now - t > breakDuration)
        {
            failures.TryDequeue(out _);
        }
        if (failures.Count >= failureThreshold)
        {
            openUntil = now + breakDuration;
        }
    }
}

public sealed class KyrolusMartenCompositeResiliencePolicy(IEnumerable<IKyrolusMartenResiliencePolicy> policies) : IKyrolusMartenResiliencePolicy
{
    private readonly IKyrolusMartenResiliencePolicy[] policies = policies?.ToArray() ?? throw new ArgumentNullException(nameof(policies));

    public Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default)
    {
        Func<Task<T>> current = action;
        for (var i = policies.Length - 1; i >= 0; i--)
        {
            var policy = policies[i];
            var inner = current;
            current = () => policy.ExecuteAsync(operation, inner, cancellationToken);
        }
        return current();
    }

    public Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default)
    {
        Func<Task> current = action;
        for (var i = policies.Length - 1; i >= 0; i--)
        {
            var policy = policies[i];
            var inner = current;
            current = () => policy.ExecuteAsync(operation, inner, cancellationToken);
        }
        return current();
    }
}
