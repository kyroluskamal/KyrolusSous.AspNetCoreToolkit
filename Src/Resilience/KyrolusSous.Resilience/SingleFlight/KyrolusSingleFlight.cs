using System.Collections.Concurrent;

namespace KyrolusSous.Resilience;

/// <summary>
/// High-performance thread-safe implementation of <see cref="IKyrolusSingleFlight"/> for request coalescing.
/// Prevents cache stampede / thundering herd by deduplicating concurrent in-flight executions for identical keys.
/// </summary>
public class KyrolusSingleFlight : IKyrolusSingleFlight
{
    private sealed class FlightCall<T>
    {
        public Task<T>? Task;
    }

    private readonly ConcurrentDictionary<(string Key, Type ResultType), object> _flights = new();

    public async Task<TResult> DoAsync<TResult>(
        string key,
        Func<CancellationToken, Task<TResult>> factory,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(factory);

        var flightKey = (key, typeof(TResult));
        var flight = (FlightCall<TResult>)_flights.GetOrAdd(flightKey, _ => new FlightCall<TResult>());

        Task<TResult> task;
        lock (flight)
        {
            if (flight.Task is null)
            {
                flight.Task = ExecuteAndCleanupAsync(flightKey, factory);
            }
            task = flight.Task;
        }

        // Await with the caller's specific cancellation token without aborting the shared background operation
        return await task.WaitAsync(cancellationToken);
    }

    private async Task<TResult> ExecuteAndCleanupAsync<TResult>(
        (string Key, Type ResultType) flightKey,
        Func<CancellationToken, Task<TResult>> factory)
    {
        try
        {
            return await factory(CancellationToken.None);
        }
        finally
        {
            _flights.TryRemove(flightKey, out _);
        }
    }
}
