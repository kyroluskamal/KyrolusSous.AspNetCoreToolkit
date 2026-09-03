using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Command.SoftDelete;

public sealed class RestoreByIdCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<RestoreByIdCommand<TResponse, TKey>, bool>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<bool> Handle(RestoreByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var keyValues = command.KeyValues;
        if (keyValues is null || keyValues.Length == 0)
        {
            throw new ArgumentException("Key values are required.", nameof(command));
        }

        bool restored;
        if (keyValues.Length == 1)
        {
            IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey> singleRepo;
            try
            {
                singleRepo = unitOfWork.GetRepository<IKyrolusSingleKeySoftDeleteRepository<TResponse, TKey>>();
            }
            catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
            {
                return false;
            }

            if (keyValues[0] is not TKey typedKey)
            {
                throw new ArgumentException("Key value type mismatch.", nameof(command));
            }

            restored = await singleRepo.RestoreAsync(typedKey, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            IKyrolusCompositeKeySoftDeleteRepository<TResponse> compositeRepo;
            try
            {
                compositeRepo = unitOfWork.GetRepository<IKyrolusCompositeKeySoftDeleteRepository<TResponse>>();
            }
            catch (InvalidOperationException ex) when (ex.IsRepositoryNotRegistered())
            {
                return false;
            }

            restored = await compositeRepo.RestoreAsync(keyValues, cancellationToken).ConfigureAwait(false);
        }

        if (restored)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return restored;
    }
}
