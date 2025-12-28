namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenResiliencePolicy
{
    Task<T> ExecuteAsync<T>(string operation, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task ExecuteAsync(string operation, Func<Task> action, CancellationToken cancellationToken = default);
}
