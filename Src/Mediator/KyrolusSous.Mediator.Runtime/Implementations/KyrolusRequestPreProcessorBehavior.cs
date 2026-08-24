namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Runs every registered <see cref="IKyrolusRequestPreProcessor{TRequest}"/> before the handler.
/// </summary>
[PipelineOrder(-1000)]
public sealed class KyrolusRequestPreProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestPreProcessor<TRequest>> preProcessors)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusRequestPreProcessor<TRequest>> _preProcessors =
        preProcessors as IReadOnlyList<IKyrolusRequestPreProcessor<TRequest>> ?? [.. preProcessors];

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        foreach (var processor in _preProcessors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await processor.Process(request, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return await next(cancellationToken).ConfigureAwait(false);
    }
}
