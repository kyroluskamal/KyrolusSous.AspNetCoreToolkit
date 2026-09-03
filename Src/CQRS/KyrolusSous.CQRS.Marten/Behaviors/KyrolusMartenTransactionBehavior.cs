using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Marten;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Marten.Behaviors;

/// <summary>
/// Pipeline behavior automatically persisting atomic changes on Marten <see cref="IDocumentSession"/> after command execution.
/// </summary>
/// <remarks>
/// Order is -530, deliberately INNER of the DomainEventsDispatch(-650)/ReadModelProjection(-600)/
/// LivePush(-550) cluster (lower number = outer). Those three behaviors run their post-`next()`
/// side effects (dispatch/project/broadcast) only after control returns to them from whatever is
/// nested inside — so Transaction must be the innermost of the group for its SaveChangesAsync to
/// happen BEFORE those side effects fire, not after. With Transaction at -700 (outermost) those
/// behaviors dispatched events/broadcasts before the session was ever flushed to Postgres, so a
/// failed SaveChangesAsync could still leave subscribers having observed a write that never persisted.
/// </remarks>
[PipelineOrder(-530)]
public sealed class KyrolusMartenTransactionBehavior<TRequest, TResponse>(
    IDocumentSession? session = null,
    ILogger<KyrolusMartenTransactionBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IDocumentSession? _session = session;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        if (request is not IKyrolusCommandBase || _session is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (request is ITransactionalCommand { DisableAutoTransaction: true })
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var response = await next(cancellationToken).ConfigureAwait(false);
        await _session.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return response;
    }
}
