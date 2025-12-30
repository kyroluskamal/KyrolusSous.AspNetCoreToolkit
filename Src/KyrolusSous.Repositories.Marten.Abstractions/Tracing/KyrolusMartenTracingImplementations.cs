using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

namespace KyrolusSous.Repositories.Marten.Abstractions.Tracing;

public sealed class KyrolusMartenNoopTracing : IKyrolusMartenTracing
{
    public static readonly IKyrolusMartenTracing Instance = new KyrolusMartenNoopTracing();

    public IDisposable? StartScope(string operation, object? payload = null) => null;

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class KyrolusMartenDelegateTracing(
    Func<string, object?, IDisposable?>? start = null,
    Func<string, object?, TimeSpan, Exception?, CancellationToken, Task>? record = null,
    Func<ValueTask>? dispose = null) : IKyrolusMartenTracing
{
    private readonly Func<string, object?, IDisposable?> start = start ?? ((_, _) => null);
    private readonly Func<string, object?, TimeSpan, Exception?, CancellationToken, Task> record = record ?? ((_, _, _, _, _) => Task.CompletedTask);
    private readonly Func<ValueTask>? dispose = dispose;

    public IDisposable? StartScope(string operation, object? payload = null)
        => start(operation, payload);

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => record(operation, payload, elapsed, exception, cancellationToken);

    public ValueTask DisposeAsync()
        => dispose is null ? ValueTask.CompletedTask : dispose();
}

public sealed class KyrolusMartenActivityTracing(string sourceName = "KyrolusSous.Marten", Func<object?, IEnumerable<KeyValuePair<string, object?>>>? tagFactory = null) : IKyrolusMartenTracing
{
    private readonly ActivitySource source = new ActivitySource(sourceName);
    private readonly Func<object?, IEnumerable<KeyValuePair<string, object?>>>? tagFactory = tagFactory;

    public IDisposable? StartScope(string operation, object? payload = null)
    {
        var activity = source.StartActivity(operation, ActivityKind.Internal);
        if (activity is null) return null;
        activity.SetTag("operation", operation);
        if (payload is not null) activity.SetTag("payload.type", payload.GetType().FullName);
        if (tagFactory is not null)
        {
            foreach (var tag in tagFactory(payload))
            {
                activity.SetTag(tag.Key, tag.Value);
            }
        }
        return new ActivityScope(activity);
    }

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("elapsed.ms", elapsed.TotalMilliseconds);
            if (exception is not null)
            {
                activity.SetStatus(ActivityStatusCode.Error, exception.Message);
                activity.SetTag("exception.type", exception.GetType().FullName);
                activity.SetTag("exception.message", exception.Message);
            }
            else
            {
                activity.SetStatus(ActivityStatusCode.Ok);
            }
        }
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        source.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class ActivityScope(Activity activity) : IDisposable
    {
        public void Dispose() => activity.Stop();
    }
}

public sealed class KyrolusMartenDebugTracing : IKyrolusMartenTracing
{
    public IDisposable? StartScope(string operation, object? payload = null)
    {
        Debug.WriteLine($"[MartenTrace] Start {operation}");
        return null;
    }

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        var status = exception is null ? "OK" : "ERROR";
        Debug.WriteLine($"[MartenTrace] End {operation} in {elapsed.TotalMilliseconds} ms ({status})");
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed record KyrolusMartenTraceRecord(
    string Operation,
    object? Payload,
    TimeSpan Elapsed,
    Exception? Exception,
    DateTimeOffset Timestamp);

public sealed class KyrolusMartenInMemoryTracing : IKyrolusMartenTracing
{
    private ConcurrentQueue<KyrolusMartenTraceRecord> records = new();

    public IDisposable? StartScope(string operation, object? payload = null) => null;

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        records.Enqueue(new KyrolusMartenTraceRecord(operation, payload, elapsed, exception, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public IReadOnlyCollection<KyrolusMartenTraceRecord> Snapshot()
        => [.. records];

    public void Reset()
    {
        Interlocked.Exchange(ref records, new ConcurrentQueue<KyrolusMartenTraceRecord>());
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

public sealed class KyrolusMartenOperationFilterTracing(Func<string, bool> predicate, IKyrolusMartenTracing inner) : IKyrolusMartenTracing
{
    private readonly Func<string, bool> predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
    private readonly IKyrolusMartenTracing inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IDisposable? StartScope(string operation, object? payload = null)
        => predicate(operation) ? inner.StartScope(operation, payload) : null;

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => predicate(operation) ? inner.RecordAsync(operation, payload, elapsed, exception, cancellationToken) : Task.CompletedTask;

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

public sealed class KyrolusMartenErrorOnlyTracing(IKyrolusMartenTracing inner) : IKyrolusMartenTracing
{
    private readonly IKyrolusMartenTracing inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IDisposable? StartScope(string operation, object? payload = null) => null;

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => exception is null ? Task.CompletedTask : inner.RecordAsync(operation, payload, elapsed, exception, cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

public sealed class KyrolusMartenThresholdTracing(TimeSpan threshold, IKyrolusMartenTracing inner) : IKyrolusMartenTracing
{
    private readonly TimeSpan threshold = threshold;
    private readonly IKyrolusMartenTracing inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public IDisposable? StartScope(string operation, object? payload = null) => null;

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => elapsed < threshold ? Task.CompletedTask : inner.RecordAsync(operation, payload, elapsed, exception, cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

public sealed class KyrolusMartenSamplingTracing : IKyrolusMartenTracing
{
    private readonly double sampleRate;
    private readonly IKyrolusMartenTracing inner;
    private readonly Random random;
    private readonly AsyncLocal<bool?> sampled = new();

    public KyrolusMartenSamplingTracing(double sampleRate, IKyrolusMartenTracing inner, Random? random = null)
    {
        if (sampleRate < 0 || sampleRate > 1) throw new ArgumentOutOfRangeException(nameof(sampleRate));
        this.sampleRate = sampleRate;
        this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
        this.random = random ?? new Random();
    }

    public IDisposable? StartScope(string operation, object? payload = null)
    {
        var previous = sampled.Value;
        var isSampled = random.NextDouble() <= sampleRate;
        sampled.Value = isSampled;
        var innerScope = isSampled ? inner.StartScope(operation, payload) : null;
        return new SamplingScope(
            () =>
            {
                innerScope?.Dispose();
                sampled.Value = previous;
            });
    }

    public Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
        => sampled.Value == true
            ? inner.RecordAsync(operation, payload, elapsed, exception, cancellationToken)
            : Task.CompletedTask;

    public ValueTask DisposeAsync() => inner.DisposeAsync();

    private sealed class SamplingScope(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

public sealed class KyrolusMartenCompositeTracing(IEnumerable<IKyrolusMartenTracing> tracers) : IKyrolusMartenTracing
{
    private readonly IKyrolusMartenTracing[] tracers = tracers?.ToArray() ?? throw new ArgumentNullException(nameof(tracers));

    public IDisposable? StartScope(string operation, object? payload = null)
    {
        var scopes = new List<IDisposable?>(tracers.Length);
        foreach (var tracer in tracers)
        {
            scopes.Add(tracer.StartScope(operation, payload));
        }
        return new CompositeScope(scopes);
    }

    public async Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default)
    {
        foreach (var tracer in tracers)
        {
            await tracer.RecordAsync(operation, payload, elapsed, exception, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var tracer in tracers)
        {
            await tracer.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class CompositeScope(List<IDisposable?> scopes) : IDisposable
    {
        public void Dispose()
        {
            foreach (var scope in scopes)
            {
                scope?.Dispose();
            }
        }
    }
}
