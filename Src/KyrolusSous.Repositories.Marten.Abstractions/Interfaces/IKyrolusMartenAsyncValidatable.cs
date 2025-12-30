namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenAsyncValidatable
{
    Task ValidateAsync(CancellationToken cancellationToken = default);
}
