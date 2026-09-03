namespace KyrolusSous.CQRS.Marten.Command.Update;

/// <remarks>
/// <see cref="Update.UpdateCommand{TResponse}.ExpectedVersion"/> is Marten's own idiom for optimistic
/// concurrency - passed to <c>session.UpdateExpectedVersion</c>, which Marten checks at
/// <c>SaveChangesAsync</c> time. EF's provider has no equivalent parameter because EF's mechanism
/// works differently (the concurrency-token value travels on the entity itself, read when it was
/// loaded); see <c>KyrolusSous.CQRS.EF.Command.Update.UpdateCommandHandler</c>'s remarks for that side.
/// Both surface a lost check by letting their provider's own exception propagate uncaught rather than
/// wrapping it: <c>KyrolusSous.ExceptionHandling.Marten</c>'s <c>KyrolusMartenExceptionMapper</c>
/// (registered via <c>AddKyrolusMartenExceptionHandling()</c>) already maps Marten's concurrency
/// exception to the same HTTP 409 <c>concurrency_conflict</c> response the EF mapper produces - that is
/// where cross-provider parity actually lives, not in the command shape.
/// </remarks>
public class UpdateCommandHandler<TSession, TResponse, TKey>(IKyrolusMartenUnitOfWork<TSession> unitOfWork)
: IKyrolusCommandHandler<UpdateCommand<TResponse>, TResponse>
     where TSession : class, IDocumentSession
    where TResponse : class
    where TKey : IEquatable<TKey>
{
    public async Task<TResponse> Handle(UpdateCommand<TResponse> command, CancellationToken cancellationToken)
    {
        var repo = unitOfWork.GetRepository<IKyrolusMartenRepositoryAsync<TSession, TResponse, TKey>>();
        var entity = await repo.UpdateAsync(command.Entity, command.ExpectedVersion, command.TenantId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return entity!;
    }
}
