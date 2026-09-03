namespace KyrolusSous.CQRS.EF.Command.Update;

/// <remarks>
/// Optimistic concurrency here works through the entity itself, not a separate parameter the way
/// Marten's <c>UpdateCommand.ExpectedVersion</c> does: <see cref="Update.UpdateCommand{TResponse}.Entity"/>
/// must be an entity the caller loaded earlier (carrying the row-version/concurrency-token value EF
/// read at that time, when the repository's policy configures one via <c>RowVersionProperty</c>), and
/// EF's own change tracker compares that value against what is actually in the database when
/// <c>SaveChangesAsync</c> runs - Marten's explicit parameter and EF's value-on-the-entity model are
/// the two ORMs' own idiomatic mechanisms for the same check, not one provider having the feature and
/// the other missing it.
/// <para>
/// A lost check is deliberately left to propagate here as the raw <see cref="DbUpdateConcurrencyException"/>
/// rather than caught and rewrapped: <c>KyrolusSous.ExceptionHandling.EntityFramework</c>'s
/// <c>KyrolusEfExceptionMapper</c> (registered via <c>AddKyrolusEntityFrameworkExceptionHandling()</c>)
/// already matches this exact type and maps it to HTTP 409 with the same <c>concurrency_conflict</c>
/// error code that <c>KyrolusSous.ExceptionHandling.Marten</c>'s <c>KyrolusMartenExceptionMapper</c>
/// maps Marten's own concurrency exception to - cross-provider parity already exists at that layer.
/// Catching and rethrowing a different exception type here would only break that mapper's type check.
/// </para>
/// </remarks>
public class UpdateCommandHandler<TDbcontext, TResponse, TKey>(IKyrolusUnitOfWork unitOfWork)
: IKyrolusCommandHandler<UpdateCommand<TResponse>, TResponse>
     where TDbcontext : DbContext
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(UpdateCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusRepositoryAsync<TDbcontext, TResponse, TKey>>();
        var entity = await repo.UpdateAsync(command.Entity, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity!;
    }
}
