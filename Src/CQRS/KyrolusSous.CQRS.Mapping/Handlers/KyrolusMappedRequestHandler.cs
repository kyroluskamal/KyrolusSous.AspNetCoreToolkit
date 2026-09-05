namespace KyrolusSous.CQRS.Mapping.Handlers;

/// <summary>
/// Abstract base handler for requests whose domain execution produces an underlying model <typeparamref name="TSource"/>
/// that is automatically mapped into the final response type <typeparamref name="TResponse"/>.
/// </summary>
/// <typeparam name="TRequest">The incoming request type.</typeparam>
/// <typeparam name="TSource">The domain entity or intermediate source type.</typeparam>
/// <typeparam name="TResponse">The final mapped response type.</typeparam>
public abstract class KyrolusMappedRequestHandler<TRequest, TSource, TResponse>(IKyrolusObjectMapper mapper)
    : IKyrolusRequestHandler<TRequest, TResponse>
    where TRequest : IKyrolusRequest<TResponse>
{
    /// <summary>
    /// The object mapper instance.
    /// </summary>
    protected readonly IKyrolusObjectMapper Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    /// <inheritdoc />
    public virtual async Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var source = await HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        if (source is null)
        {
            // For a non-nullable value-type TResponse (bool, int, a struct), default! is a real value
            // (false, 0, ...), not an absence signal - a caller checking the result for "not found"
            // would silently see a valid-looking default instead. Only a reference type or Nullable<T>
            // can represent "no response" unambiguously, so require one of those for this convention.
            if (typeof(TResponse).IsValueType && Nullable.GetUnderlyingType(typeof(TResponse)) is null)
            {
                throw new InvalidOperationException(
                    $"{GetType().Name}.Handle received a null {typeof(TSource).Name} from HandleCoreAsync, " +
                    $"but the response type '{typeof(TResponse).Name}' is a non-nullable value type with no " +
                    "representation for \"not found\". Use a reference type or a nullable value type " +
                    $"(e.g. '{typeof(TResponse).Name}?') as TResponse, or override Handle to define custom null-handling.");
            }

            return default!;
        }

        var context = CreateMappingContext(request);
        return context is not null
            ? Mapper.Map<TSource, TResponse>(source, context)
            : Mapper.Map<TSource, TResponse>(source);
    }

    /// <summary>
    /// Executes the underlying domain logic or data retrieval producing <typeparamref name="TSource"/>.
    /// </summary>
    /// <param name="request">The incoming request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The source domain instance.</returns>
    protected abstract Task<TSource> HandleCoreAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Optional hook to build a <see cref="KyrolusMappingContext"/> with custom parameters for this request.
    /// </summary>
    protected virtual KyrolusMappingContext? CreateMappingContext(TRequest request) => null;
}

/// <summary>
/// Abstract base handler for requests producing a <see cref="KyrolusPagedResult{TResponse}"/> by auto-mapping from
/// an underlying <see cref="KyrolusPagedResult{TSource}"/>.
/// </summary>
/// <typeparam name="TRequest">The incoming request type.</typeparam>
/// <typeparam name="TSource">The domain entity or item source type.</typeparam>
/// <typeparam name="TResponse">The final mapped item response type.</typeparam>
public abstract class KyrolusMappedPagedRequestHandler<TRequest, TSource, TResponse>(IKyrolusObjectMapper mapper)
    : IKyrolusRequestHandler<TRequest, KyrolusPagedResult<TResponse>>
    where TRequest : IKyrolusRequest<KyrolusPagedResult<TResponse>>
{
    /// <summary>
    /// The object mapper instance.
    /// </summary>
    protected readonly IKyrolusObjectMapper Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    /// <inheritdoc />
    public virtual async Task<KyrolusPagedResult<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var pagedSource = await HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(pagedSource);

        var context = CreateMappingContext(request);
        return pagedSource.MapTo<TSource, TResponse>(Mapper, context);
    }

    /// <summary>
    /// Executes the underlying domain logic producing the source paged result.
    /// </summary>
    protected abstract Task<KyrolusPagedResult<TSource>> HandleCoreAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Optional hook to build a <see cref="KyrolusMappingContext"/> for this request.
    /// </summary>
    protected virtual KyrolusMappingContext? CreateMappingContext(TRequest request) => null;
}

/// <summary>
/// Abstract base handler for requests producing a <see cref="KyrolusSeekResult{TResponse}"/> by auto-mapping from
/// an underlying <see cref="KyrolusSeekResult{TSource}"/>.
/// </summary>
/// <typeparam name="TRequest">The incoming request type.</typeparam>
/// <typeparam name="TSource">The domain entity or item source type.</typeparam>
/// <typeparam name="TResponse">The final mapped item response type.</typeparam>
public abstract class KyrolusMappedSeekRequestHandler<TRequest, TSource, TResponse>(IKyrolusObjectMapper mapper)
    : IKyrolusRequestHandler<TRequest, KyrolusSeekResult<TResponse>>
    where TRequest : IKyrolusRequest<KyrolusSeekResult<TResponse>>
{
    /// <summary>
    /// The object mapper instance.
    /// </summary>
    protected readonly IKyrolusObjectMapper Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    /// <inheritdoc />
    public virtual async Task<KyrolusSeekResult<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var seekSource = await HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(seekSource);

        var context = CreateMappingContext(request);
        return seekSource.MapTo<TSource, TResponse>(Mapper, context);
    }

    /// <summary>
    /// Executes the underlying domain logic producing the source seek result.
    /// </summary>
    protected abstract Task<KyrolusSeekResult<TSource>> HandleCoreAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Optional hook to build a <see cref="KyrolusMappingContext"/> for this request.
    /// </summary>
    protected virtual KyrolusMappingContext? CreateMappingContext(TRequest request) => null;
}

/// <summary>
/// Abstract base handler for requests producing an <see cref="IReadOnlyList{TResponse}"/> by auto-mapping from
/// an underlying <see cref="IEnumerable{TSource}"/>.
/// </summary>
/// <typeparam name="TRequest">The incoming request type.</typeparam>
/// <typeparam name="TSource">The domain entity or item source type.</typeparam>
/// <typeparam name="TResponse">The final mapped item response type.</typeparam>
public abstract class KyrolusMappedListRequestHandler<TRequest, TSource, TResponse>(IKyrolusObjectMapper mapper)
    : IKyrolusRequestHandler<TRequest, IReadOnlyList<TResponse>>
    where TRequest : IKyrolusRequest<IReadOnlyList<TResponse>>
{
    /// <summary>
    /// The object mapper instance.
    /// </summary>
    protected readonly IKyrolusObjectMapper Mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));

    /// <inheritdoc />
    public virtual async Task<IReadOnlyList<TResponse>> Handle(TRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var listSource = await HandleCoreAsync(request, cancellationToken).ConfigureAwait(false);
        ArgumentNullException.ThrowIfNull(listSource);

        var context = CreateMappingContext(request);
        return listSource.ToDtoList<TSource, TResponse>(Mapper, context);
    }

    /// <summary>
    /// Executes the underlying domain logic producing the source collection.
    /// </summary>
    protected abstract Task<IEnumerable<TSource>> HandleCoreAsync(TRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Optional hook to build a <see cref="KyrolusMappingContext"/> for this request.
    /// </summary>
    protected virtual KyrolusMappingContext? CreateMappingContext(TRequest request) => null;
}
