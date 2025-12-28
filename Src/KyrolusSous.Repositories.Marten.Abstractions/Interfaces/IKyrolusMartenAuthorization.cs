namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenAuthorization
{
    Task<bool> AuthorizeAsync(string operation, object? target, CancellationToken cancellationToken = default);
}
