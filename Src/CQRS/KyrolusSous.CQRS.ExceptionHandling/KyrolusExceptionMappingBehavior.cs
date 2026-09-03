using KyrolusSous.Mediator.Abstractions;
using KyrolusSous.Mediator.Abstractions.Attributes;

namespace KyrolusSous.CQRS.ExceptionHandling;

// Must wrap OUTSIDE (more negative than) KyrolusRequestExceptionProcessorBehavior, which stays at
// -2000 in KyrolusSous.Mediator.Runtime. That behavior runs its registered
// IKyrolusRequestExceptionAction/IKyrolusRequestExceptionHandler implementations (logging,
// alerting, recovery) first; only an exception neither of those recovers should ever reach this
// CQRS-level exception-to-response mapping, as the true last line of defense.
[PipelineOrder(-2100)]
public sealed class KyrolusExceptionMappingBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusExceptionMapper<TResponse>>? mappers = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusExceptionMapper<TResponse>> _mappers =
        mappers as IReadOnlyList<IKyrolusExceptionMapper<TResponse>> ?? (mappers is not null ? [.. mappers] : []);

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
        catch (Exception ex)
        {
            foreach (var mapper in _mappers)
            {
                if (mapper.TryMap(ex, out var mapped))
                {
                    return mapped;
                }
            }

            throw;
        }
    }
}
