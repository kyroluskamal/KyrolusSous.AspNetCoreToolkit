using KyrolusSous.Repositories.EF.Abstractions.Helpers;

namespace KyrolusSous.CQRS.Marten.Command.Bulk;

public sealed class BulkPatchCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<BulkPatchCommand<TResponse, TKey>, int>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<int> Handle(BulkPatchCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var keyProps = ResolveKeyProperties(command);
        var total = 0;

        foreach (var item in command.Items)
        {
            var filter = KyrolusEFRepositoryBase<TResponse>.GetPrimaryKeyFromKeyValues(item.KeyValues, keyProps);
            total += await repo.PatchWhereAsync(filter, item.Updates, command.TenantId, cancellationToken).ConfigureAwait(false);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return total;
    }

    private static string[] ResolveKeyProperties(BulkPatchCommand<TResponse, TKey> command)
    {
        if (command.KeyPropertyNames is { Count: > 0 })
        {
            return command.KeyPropertyNames.Where(static p => !string.IsNullOrWhiteSpace(p)).ToArray();
        }

        return ["Id"];
    }
}
