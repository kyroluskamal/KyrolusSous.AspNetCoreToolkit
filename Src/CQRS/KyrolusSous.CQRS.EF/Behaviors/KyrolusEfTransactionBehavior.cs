using KyrolusSous.Mediator.Abstractions.Attributes;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.EF.Behaviors;

/// <summary>
/// Pipeline behavior managing atomic EF Core transaction boundaries for commands.
/// </summary>
/// <remarks>
/// Order is -530, deliberately INNER of the DomainEventsDispatch(-650)/ReadModelProjection(-600)/
/// LivePush(-550) cluster (lower number = outer). Those three behaviors run their post-`next()`
/// side effects (dispatch/project/broadcast) only after control returns to them from whatever is
/// nested inside — so Transaction must be the innermost of the group for its commit to happen
/// BEFORE those side effects fire, not after. With Transaction at -700 (outermost) those behaviors
/// dispatched events/broadcasts before the transaction actually committed, so a failed commit could
/// still leave subscribers having observed a write that never persisted.
/// </remarks>
[PipelineOrder(-530)]
public sealed class KyrolusEfTransactionBehavior<TRequest, TResponse, TDbContext>(
    TDbContext? dbContext = null,
    ILogger<KyrolusEfTransactionBehavior<TRequest, TResponse, TDbContext>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
    where TDbContext : DbContext
{
    private readonly TDbContext? _dbContext = dbContext;
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        // Only manage transactions for commands
        if (request is not IKyrolusCommandBase || _dbContext is null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        if (request is IKyrolusTransactionalCommand { DisableAutoTransaction: true })
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        // If an ambient EF transaction is already in progress, avoid nested transaction creation
        if (_dbContext.Database.CurrentTransaction is not null)
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            _logger?.LogDebug("[Kyrolus CQRS EF] Began transaction for command '{CommandType}'", typeof(TRequest).Name);

            await using (transaction)
            {
                try
                {
                    var response = await next(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    _logger?.LogDebug("[Kyrolus CQRS EF] Committed transaction for command '{CommandType}'", typeof(TRequest).Name);
                    return response;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[Kyrolus CQRS EF] Transaction failed and rolled back for command '{CommandType}'", typeof(TRequest).Name);
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw new InvalidOperationException(
                        $"The EF transaction failed for command '{typeof(TRequest).Name}'.",
                        ex);
                }
            }
        }).ConfigureAwait(false);
    }
}
