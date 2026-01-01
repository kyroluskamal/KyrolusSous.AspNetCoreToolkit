using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveByIdCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork) : IKyrolusCommandHandler<RemoveByIdCommand<TResponse, TKey>>
    where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public virtual async Task Handle(RemoveByIdCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        await repo.RemoveAsync(command.KeyValues, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
