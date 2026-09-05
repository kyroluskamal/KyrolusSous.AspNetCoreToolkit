using KyrolusSous.CQRS.EF.Command.Bulk;

namespace KyrolusSous.CQRS.EF.Command.Patch;


public class PatchCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
: IKyrolusCommandHandler<PatchCommand<TResponse, TKey>, TResponse?>
     where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse?> Handle(PatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        // Always-on, independent of IKyrolusPropertyUpdateRequest.AllowedProperties (which is opt-in
        // and does nothing when a caller never sets it): a key, concurrency-token, or DB-computed
        // column must never be writable through Patch regardless of allow-list configuration.
        // Mirrors ExecuteUpdateCommandHandler's guard for the same Dictionary<string, object> input
        // shape - see EfProtectedPropertyGuard.
        foreach (var name in command.Updates.Keys)
        {
            var prop = typeof(TResponse).GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (prop is not null)
            {
                EfProtectedPropertyGuard.ThrowIfProtected(prop, typeof(TResponse), "Patch");
            }
        }

        var repo = unitOfWork.GetRepository<IKyrolusCompositeKeyRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entity = await repo.PatchAsync(command.KeyValues, command.Updates, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity;
    }
}
