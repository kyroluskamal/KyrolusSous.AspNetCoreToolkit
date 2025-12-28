
namespace KyrolusSous.Repositories.EF.Abstractions.Observer;

public sealed class SampleObserver : IKyrolusRepositoryObserver
{
    public Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default)
    {
        // Hook metrics/logging/caching signals here
        Debug.WriteLine($"[RepoObserver] Starting {operation}");
        return Task.CompletedTask;
    }

    public Task OnAfterAsync(string operation, object? payload, TimeSpan? duration = null, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        Debug.WriteLine($"[RepoObserver] Finished {operation} in {duration?.TotalMilliseconds} ms {(exception is null ? "OK" : "ERROR")}");
        return Task.CompletedTask;
    }
}
