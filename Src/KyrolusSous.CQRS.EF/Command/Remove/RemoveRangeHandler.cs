using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace KyrolusSous.CQRS.EF.Command.Remove;

public class RemoveRangeHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork) :
 IKyrolusCommandHandler<RemoveRangeCommand<TResponse>>
 where TDbcontext : DbContext
 where TResponse : class
 where TKey : IEquatable<TKey>
{
    public async Task Handle(RemoveRangeCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        await repo.RemoveRangeAsync(command.Entities, cancellationToken: cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
