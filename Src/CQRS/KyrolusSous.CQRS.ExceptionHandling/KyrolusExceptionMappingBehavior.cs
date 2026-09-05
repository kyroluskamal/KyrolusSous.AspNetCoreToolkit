using Microsoft.Extensions.Logging;

namespace KyrolusSous.CQRS.ExceptionHandling;

// Must wrap OUTSIDE (more negative than) KyrolusRequestExceptionProcessorBehavior, which stays at
// -2000 in KyrolusSous.Mediator.Runtime. That behavior runs its registered
// IKyrolusRequestExceptionAction/IKyrolusRequestExceptionHandler implementations (logging,
// alerting, recovery) first; only an exception neither of those recovers should ever reach this
// CQRS-level exception-to-response mapping, as the true last line of defense.
[PipelineOrder(-2100)]
public sealed class KyrolusExceptionMappingBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusExceptionMapper<TResponse>>? mappers = null,
    ILogger<KyrolusExceptionMappingBehavior<TRequest, TResponse>>? logger = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusExceptionMapper<TResponse>> _mappers =
        mappers as IReadOnlyList<IKyrolusExceptionMapper<TResponse>> ?? (mappers is not null ? [.. mappers] : []);
    private readonly ILogger? _logger = logger;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Cancellation is not a failure a mapper should get to translate into an ordinary mapped
            // response - a mapper broad enough to match Exception in general (a common "catch-all"
            // implementation) would otherwise silently turn a cancelled request into a normal-looking
            // result instead of letting the cancellation propagate to whoever asked for it.
            throw;
        }
        catch (Exception ex)
        {
            foreach (var mapper in _mappers)
            {
                try
                {
                    if (mapper.TryMap(ex, out var mapped))
                    {
                        return mapped;
                    }
                }
                catch (Exception mapperEx)
                {
                    // A mapper that itself throws must not be allowed to replace the original
                    // exception - this behavior is the last line of defense, and losing ex's identity
                    // and stack trace here means whatever propagates next tells the caller (and the
                    // logs) about a bug in a mapper instead of the actual failure the mapper was
                    // asked to translate. Log the mapper's failure against the original exception and
                    // keep trying the remaining mappers.
                    _logger?.LogError(
                        mapperEx,
                        "[Kyrolus CQRS] Exception mapper {MapperType} threw while handling {OriginalExceptionType} for {RequestType}.",
                        mapper.GetType().Name,
                        ex.GetType().Name,
                        typeof(TRequest).Name);
                }
            }

            throw;
        }
    }
}
