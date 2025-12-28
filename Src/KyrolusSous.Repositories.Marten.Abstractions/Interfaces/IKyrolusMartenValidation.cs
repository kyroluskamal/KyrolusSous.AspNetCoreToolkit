namespace KyrolusSous.Repositories.Marten.Abstractions.Interfaces;

public interface IKyrolusMartenValidation
{
    Task ValidateAsync(string operation, object? payload, CancellationToken cancellationToken = default);
}
