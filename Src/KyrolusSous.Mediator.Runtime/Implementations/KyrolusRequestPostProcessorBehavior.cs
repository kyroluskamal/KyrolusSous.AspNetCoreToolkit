namespace KyrolusSous.Mediator.Runtime.Implementations;

[PipelineOrder(1000)]
public sealed class KyrolusRequestPostProcessorBehavior<TRequest, TResponse>(
    IEnumerable<IKyrolusRequestPostProcessor<TRequest, TResponse>> postProcessors)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IReadOnlyList<IKyrolusRequestPostProcessor<TRequest, TResponse>> _postProcessors =
        postProcessors as IReadOnlyList<IKyrolusRequestPostProcessor<TRequest, TResponse>> ?? postProcessors.ToList();

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var response = await next().ConfigureAwait(false);

        foreach (var processor in _postProcessors)
        {
            await processor.Process(request, response, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }
}
