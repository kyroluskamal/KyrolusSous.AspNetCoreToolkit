namespace KyrolusSous.Repositories.EF.Abstractions.Interfaces;

public interface IKyrolusRepositoryObserver
{
    Task OnBeforeAsync(string operation, object? payload, CancellationToken cancellationToken = default);
    Task OnAfterAsync(string operation, object? payload, TimeSpan? duration = null, Exception? exception = null, CancellationToken cancellationToken = default);
}
