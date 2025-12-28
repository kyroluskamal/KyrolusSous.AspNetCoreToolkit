namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenObserver
{
    Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default);
    Task OnAfterAsync(string operation, object? result, TimeSpan elapsed, Exception? exception, CancellationToken cancellationToken = default);
}
