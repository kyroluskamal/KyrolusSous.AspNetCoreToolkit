using KyrolusSous.CQRS.Abstractions.Interfaces;
using KyrolusSous.Mediator.Abstractions.Attributes;
using KyrolusSous.Mediator.Abstractions.Interfaces;
using Marten;
using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.Marten.Behaviors;

/// <summary>
/// Pipeline behavior automatically persisting atomic changes on Marten <see cref="IDocumentSession"/> after command execution.
/// </summary>
[PipelineOrder(-700)]
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
