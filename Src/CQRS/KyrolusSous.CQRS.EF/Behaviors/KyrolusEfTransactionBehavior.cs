using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.EF.Behaviors;

/// <summary>
/// Pipeline behavior managing atomic EF Core transaction boundaries for commands.
/// </summary>
[PipelineOrder(-700)]
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

        if (request is ITransactionalCommand { DisableAutoTransaction: true })
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

            await using (transaction)
            {
                try
                {
                    var response = await next(cancellationToken).ConfigureAwait(false);
                    await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return response;
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                    throw;
                }
            }
        }).ConfigureAwait(false);
    }
}
