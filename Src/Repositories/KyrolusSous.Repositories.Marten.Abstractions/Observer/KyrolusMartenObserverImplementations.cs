using System.Collections.Concurrent;
using System.Diagnostics;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Observer;

public sealed class KyrolusMartenNoopObserver : IKyrolusMartenObserver
{
    public static readonly IKyrolusMartenObserver Instance = new KyrolusMartenNoopObserver();

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}

public sealed class KyrolusMartenDelegateObserver(
    Func<string, object?, CancellationToken, Task>? onBefore = null,
    Func<string, object?, TimeSpan, Exception?, CancellationToken, Task>? onAfter = null) : IKyrolusMartenObserver
{
    private readonly Func<string, object?, CancellationToken, Task> onBefore = onBefore ?? ((_, _, _) => Task.CompletedTask);
    private readonly Func<string, object?, TimeSpan, Exception?, CancellationToken, Task> onAfter = onAfter ?? ((_, _, _, _, _) => Task.CompletedTask);

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => onBefore(operation, payload, cancellationToken);

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => onAfter(operation, result, elapsed, exception, cancellationToken);
}

public sealed class KyrolusMartenDebugObserver : IKyrolusMartenObserver
{
    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[MartenObserver] Starting {operation}");
        return Task.CompletedTask;
    }

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        var status = exception is null ? "OK" : "ERROR";
        Debug.WriteLine($"[MartenObserver] Finished {operation} in {elapsed.TotalMilliseconds} ms ({status})");
        return Task.CompletedTask;
    }
}

public sealed class KyrolusMartenErrorOnlyObserver(Func<string, object?, TimeSpan, Exception, CancellationToken, Task> onError) : IKyrolusMartenObserver
{
    private readonly Func<string, object?, TimeSpan, Exception, CancellationToken, Task> onError = onError ?? throw new ArgumentNullException(nameof(onError));

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        if (exception is null) return Task.CompletedTask;
        return onError(operation, result, elapsed, exception, cancellationToken);
    }
}

public sealed class KyrolusMartenSlowOperationObserver(TimeSpan threshold, Func<string, object?, TimeSpan, CancellationToken, Task> onSlow) : IKyrolusMartenObserver
{
    private readonly TimeSpan threshold = threshold;
    private readonly Func<string, object?, TimeSpan, CancellationToken, Task> onSlow = onSlow ?? throw new ArgumentNullException(nameof(onSlow));

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        if (elapsed < threshold) return Task.CompletedTask;
        return onSlow(operation, result, elapsed, cancellationToken);
    }
}

public sealed class KyrolusMartenOperationFilterObserver(Func<string, bool> predicate, IKyrolusMartenObserver inner) : IKyrolusMartenObserver
{
    private readonly Func<string, bool> predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    private readonly IKyrolusMartenObserver inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
        => predicate(operation) ? inner.OnBeforeAsync(operation, payload, cancellationToken) : Task.CompletedTask;

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => predicate(operation) ? inner.OnAfterAsync(operation, result, elapsed, exception, cancellationToken) : Task.CompletedTask;
}

public sealed class KyrolusMartenCompositeObserver(IEnumerable<IKyrolusMartenObserver> observers) : IKyrolusMartenObserver
{
    private readonly IKyrolusMartenObserver[] observers = observers?.ToArray() ?? throw new ArgumentNullException(nameof(observers));

    public async Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        foreach (var observer in observers)
        {
            await observer.OnBeforeAsync(operation, payload, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        foreach (var observer in observers)
        {
            await observer.OnAfterAsync(operation, result, elapsed, exception, cancellationToken).ConfigureAwait(false);
        }
    }
}

public sealed class KyrolusMartenCountingObserver(
    bool countOnBefore = false,
    bool countOnAfter = true,
    bool countFailuresOnly = false,
    StringComparer? comparer = null) : IKyrolusMartenObserver
{
    private readonly ConcurrentDictionary<string, long> counts = new ConcurrentDictionary<string, long>(comparer ?? StringComparer.OrdinalIgnoreCase);
    private readonly bool countOnBefore = countOnBefore;
    private readonly bool countOnAfter = countOnAfter;
    private readonly bool countFailuresOnly = countFailuresOnly;

    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        if (countOnBefore) Increment(operation);
        return Task.CompletedTask;
    }

    public Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        if (!countOnAfter) return Task.CompletedTask;
        if (countFailuresOnly && exception is null) return Task.CompletedTask;
        Increment(operation);
        return Task.CompletedTask;
    }

    public IReadOnlyDictionary<string, long> Snapshot()
        => new Dictionary<string, long>(counts);

    public void Reset() => counts.Clear();

    private void Increment(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation)) return;
        counts.AddOrUpdate(operation, 1, (_, current) => current + 1);
    }
}
