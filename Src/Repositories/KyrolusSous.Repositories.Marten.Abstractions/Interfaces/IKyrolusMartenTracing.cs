namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenTracing : IAsyncDisposable
{
    IDisposable? StartScope(string operation, object? payload = null);
    Task RecordAsync(string operation, object? payload, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default);
}
