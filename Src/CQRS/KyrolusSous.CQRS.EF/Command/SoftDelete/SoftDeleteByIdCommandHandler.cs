using KyrolusSous.Mediator.Abstractions.Interfaces;
using KyrolusSous.Repositories.EF.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Command.SoftDelete;

public sealed class SoftDeleteByIdCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
    : IKyrolusCommandHandler<SoftDeleteByIdCommand<TResponse, TKey>, bool>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<bool> Handle(SoftDeleteByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var keyValues = command.KeyValues;
        if (keyValues is null || keyValues.Length == 0)
        {
            throw new ArgumentException("Key values are required.", nameof(command));
        }

        bool removed;
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

            removed = await singleRepo.SoftDeleteAsync(typedKey, cancellationToken).ConfigureAwait(false);
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

            removed = await compositeRepo.SoftDeleteAsync(keyValues, cancellationToken).ConfigureAwait(false);
        }

        if (removed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return removed;
    }
}
