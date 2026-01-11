namespace KyrolusSous.Repositories.EF.Generator.TestApp;

public sealed class TestRepositoryObserver : IKyrolusRepositoryObserver
{
    public record Event(string Stage, string Operation, object? Payload, TimeSpan? Duration, Exception? Exception);

    private readonly ConcurrentQueue<Event> _events = new();

    public IReadOnlyCollection<Event> Events => _events.ToArray();

    public void Reset() => _events.Clear();


    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(new Event("Before", operation, payload, Duration: null, Exception: null));
        return Task.CompletedTask;
    }

    public Task OnAfterAsync(
        string operation,
        object? payload,
        TimeSpan? duration = null,
        Exception? exception = null,
        CancellationToken cancellationToken = default)
    {
        _events.Enqueue(new Event("After", operation, payload, duration, exception));
        return Task.CompletedTask;
    }
}