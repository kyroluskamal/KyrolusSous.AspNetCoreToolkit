namespace KyrolusSous.CQRS.Mapping.Behaviors;

/// <summary>
/// CQRS pipeline behavior that integrates mapping context parameters and response post-processing.
/// </summary>
/// <typeparam name="TRequest">The incoming request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
[PipelineOrder(-920)]
public sealed class KyrolusMappingPipelineBehavior<TRequest, TResponse>(
    IKyrolusObjectMapper? mapper = null)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IKyrolusObjectMapper? _mapper = mapper;

    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(next);
        cancellationToken.ThrowIfCancellationRequested();

        KyrolusMappingContext? mappingContext = null;

        if (request is IKyrolusContextAwareMapping contextAware)
        {
            mappingContext = new KyrolusMappingContext();
            contextAware.ConfigureMappingContext(mappingContext);
        }

        var response = await next(cancellationToken).ConfigureAwait(false);

        if (response is IKyrolusPostMappableResponse postMappable && _mapper is not null)
            postMappable.OnMapped(_mapper, mappingContext);

        return response;
    }
}
