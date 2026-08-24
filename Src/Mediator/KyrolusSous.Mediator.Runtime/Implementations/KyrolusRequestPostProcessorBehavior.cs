namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Runs every registered <see cref="IKyrolusRequestPostProcessor{TRequest, TResponse}"/> after the
/// handler has produced a response.
/// </summary>
[PipelineOrder(1000)]
public sealed class KyrolusRequestPostProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestPostProcessor<TRequest, TResponse>> postProcessors)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusRequestPostProcessor<TRequest, TResponse>> _postProcessors =
        postProcessors as IReadOnlyList<IKyrolusRequestPostProcessor<TRequest, TResponse>> ?? [.. postProcessors];

    /// <inheritdoc />
    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);

        var response = await next(cancellationToken).ConfigureAwait(false);

        foreach (var processor in _postProcessors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await processor.Process(request, response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
