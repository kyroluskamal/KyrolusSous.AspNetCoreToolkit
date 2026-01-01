namespace KyrolusSous.Mediator.Runtime.Implementations;

[PipelineOrder(-1000)]
public sealed class KyrolusRequestPreProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestPreProcessor<TRequest>> preProcessors)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusRequestPreProcessor<TRequest>> _preProcessors =
        preProcessors as IReadOnlyList<IKyrolusRequestPreProcessor<TRequest>> ?? preProcessors.ToList();

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        foreach (var processor in _preProcessors)
        {
            await processor.Process(request, cancellationToken).ConfigureAwait(false);
        }

        return await next().ConfigureAwait(false);
    }
}
