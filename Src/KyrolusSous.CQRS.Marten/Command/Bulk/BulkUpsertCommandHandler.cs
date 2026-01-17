namespace KyrolusSous.CQRS.Marten.Command.Bulk;

public sealed class BulkUpsertCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
    : IKyrolusCommandHandler<BulkUpsertCommand<TResponse, TKey>, IEnumerable<TResponse>>
    where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<IEnumerable<TResponse>> Handle(BulkUpsertCommand<TResponse, TKey> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var result = await repo.UpsertRangeAsync(command.Entities, command.TenantId, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
