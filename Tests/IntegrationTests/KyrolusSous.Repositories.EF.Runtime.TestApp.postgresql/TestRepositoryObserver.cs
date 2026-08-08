using System;

namespace KyrolusSous.Repositories.EF.Runtime.TestApp;

using System.Collections.Concurrent;

public sealed class TestRepositoryObserver : IKyrolusRepositoryObserver
{
    public record Event(ObserverState Stage, string Operation, object? Payload, object? Result, Exception? Exception, TimeSpan? Duration = null);

    private readonly ConcurrentQueue<Event> _events = new();

    public IReadOnlyCollection<Event> Events => [.. _events];

    public void Reset() => _events.Clear();


    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(new Event(ObserverState.Before, operation, payload, null, null));
        return Task.CompletedTask;
    }

    public Task OnAfterAsync(string operation, object? payload, TimeSpan? duration = null, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        _events.Enqueue(new Event(ObserverState.After, operation, payload, null, exception, duration));
        return Task.CompletedTask;
    }
}

public enum ObserverState
{
    Before,
    After
}

