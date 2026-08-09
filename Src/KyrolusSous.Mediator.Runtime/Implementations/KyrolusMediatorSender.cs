using KyrolusSous.Mediator.Runtime.GeneratorIntegration;
using KyrolusSous.Mediator.Runtime.Internal;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Concrete implementation of <see cref="IKyrolusMediatorSender"/>.
/// Resolves the dispatcher and runs the registered pipeline behaviors around it.
/// </summary>
/// <remarks>
/// The pipeline is built once per (request type, response type) pair and cached as a closed
/// generic wrapper, so sending a request costs one dictionary lookup plus one virtual call.
/// <para>
/// Every wrapper comes from <see cref="IKyrolusPipelineWrapperSource"/> - this class never builds
/// one itself. That is what keeps it free of <c>MakeGenericType</c>, which an application published
/// ahead of time cannot use when the response is a value type. The generator supplies a source
/// built from types it saw at compile time; <c>KyrolusSous.Mediator.Reflection</c> supplies one
/// that closes the types on demand. Neither is referenced from here.
/// </para>
/// </remarks>
/// <param name="serviceProvider">The service provider instance.</param>
/// <param name="dispatcher">The dispatcher implementation (generated or reflection-based).</param>
/// <exception cref="ArgumentNullException">Thrown if serviceProvider or dispatcher is null.</exception>
public sealed class KyrolusMediatorSender(IServiceProvider serviceProvider, IMediatorDispatcher dispatcher) : IKyrolusMediatorSender
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestPipelineWrapper> s_requestWrappers = new();
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), StreamPipelineWrapper> s_streamWrappers = new();

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IMediatorDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
    private readonly IKyrolusPipelineWrapperSource? _wrapperSource = serviceProvider.GetService<IKyrolusPipelineWrapperSource>();

    // --- Typed overloads ---


    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IKyrolusQuery<TResponse> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteAsync<TResponse>(query, cancellationToken);
    }

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IKyrolusRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return ExecuteAsync<TResponse>(request, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SendAsync(IKyrolusCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        await ExecuteAsync<Unit>(command, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public Task<TResponse> SendAsync<TResponse>(IKyrolusCommand<TResponse> command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        return ExecuteAsync<TResponse>(command, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<TResponse> StreamAsync<TResponse>(IKyrolusStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var wrapper = (StreamPipelineWrapper<TResponse>)GetStreamWrapper(request.GetType(), typeof(TResponse));
        return wrapper.Handle(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    // --- Untyped overloads: the response type is discovered from the request itself ---

    /// <inheritdoc />
    public Task<object?> SendAsync(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = GetResponseType(requestType, stream: false);
        var wrapper = GetRequestWrapper(requestType, responseType);

        return wrapper.HandleUntyped(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<object?> StreamAsync(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var requestType = request.GetType();
        var responseType = GetResponseType(requestType, stream: true);
        var wrapper = GetStreamWrapper(requestType, responseType);

        return wrapper.HandleUntyped(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    // --- Internals ---
    private Task<TResponse> ExecuteAsync<TResponse>(object request, CancellationToken cancellationToken)
    {
        var wrapper = (RequestPipelineWrapper<TResponse>)GetRequestWrapper(request.GetType(), typeof(TResponse));
        return wrapper.Handle(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    private RequestPipelineWrapper GetRequestWrapper(Type requestType, Type responseType)
        => s_requestWrappers.GetOrAdd(
            (requestType, responseType),
            static (key, source) => (RequestPipelineWrapper)(
                RequireSource(source).CreateRequestWrapper(key.RequestType, key.ResponseType)
                ?? throw NoWrapper(key.RequestType, key.ResponseType)),
            _wrapperSource);

    private StreamPipelineWrapper GetStreamWrapper(Type requestType, Type responseType)
        => s_streamWrappers.GetOrAdd(
            (requestType, responseType),
            static (key, source) => (StreamPipelineWrapper)(
                RequireSource(source).CreateStreamWrapper(key.RequestType, key.ResponseType)
                ?? throw NoWrapper(key.RequestType, key.ResponseType)),
            _wrapperSource);

    private Type GetResponseType(Type requestType, bool stream)
        => RequireSource(_wrapperSource).GetResponseType(requestType, stream)
           ?? throw new ArgumentException(
               $"[KyrolusMediator] Could not determine the response type of {requestType.FullName}. " +
               "Either it does not declare one, or it declares more than one and the untyped overload " +
               "cannot choose - call the overload that names the response instead.",
               nameof(requestType));

    private static IKyrolusPipelineWrapperSource RequireSource(IKyrolusPipelineWrapperSource? source)
        => source ?? throw new InvalidOperationException(
            "[KyrolusMediator] No pipeline wrapper source is registered. Reference " +
            "KyrolusSous.Mediator.Generator and call AddKyrolusMediatorGeneratedDispatcher(), or " +
            "reference KyrolusSous.Mediator.Reflection and call AddKyrolusMediatorReflection().");

    private static InvalidOperationException NoWrapper(Type requestType, Type responseType)
        => new(
            $"[KyrolusMediator] No pipeline wrapper for {requestType.FullName} -> {responseType.FullName}. " +
            "The generator emits one per handler it can see; a handler that is generic, or declared in " +
            "another assembly, is not among them. Add KyrolusSous.Mediator.Reflection to close the " +
            "types at runtime instead, which an application published ahead of time cannot do.");
}
