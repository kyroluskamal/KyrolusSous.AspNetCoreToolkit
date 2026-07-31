using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Mediator.Runtime.Internal;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Concrete implementation of <see cref="IKyrolusMediatorSender"/>.
/// Resolves the dispatcher (generated or reflection-based) and runs the registered pipeline
/// behaviors around it.
/// </summary>
/// <remarks>
/// The pipeline is built once per (request type, response type) pair and cached as a closed
/// generic wrapper, so sending a request costs one dictionary lookup plus one virtual call -
/// not a <see cref="MethodInfo"/> invoke and an attribute scan per send.
/// </remarks>
/// <param name="serviceProvider">The service provider instance.</param>
/// <param name="dispatcher">The dispatcher implementation (generated or reflection-based).</param>
/// <exception cref="ArgumentNullException">Thrown if serviceProvider or dispatcher is null.</exception>
public sealed class KyrolusMediatorSender(IServiceProvider serviceProvider, IMediatorDispatcher dispatcher) : IKyrolusMediatorSender
{
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), RequestPipelineWrapper> s_requestWrappers = new();
    private static readonly ConcurrentDictionary<(Type RequestType, Type ResponseType), StreamPipelineWrapper> s_streamWrappers = new();

    /// <summary>Caches the response type declared by a request type, for the untyped overloads.</summary>
    private static readonly ConcurrentDictionary<(Type RequestType, Type OpenInterface), Type> s_responseTypeCache = new();

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IMediatorDispatcher _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

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

        var wrapper = (StreamPipelineWrapper<TResponse>)s_streamWrappers.GetOrAdd(
            (request.GetType(), typeof(TResponse)),
            static key => CreateWrapper<StreamPipelineWrapper>(typeof(StreamPipelineWrapperImpl<,>), key));

        return wrapper.Handle(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    // --- Untyped overloads: the response type is discovered from the request itself ---

    /// <inheritdoc />
    public Task<object?> SendAsync(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var responseType = GetResponseType(request.GetType(), typeof(IKyrolusRequest<>), "a request");
        var wrapper = s_requestWrappers.GetOrAdd(
            (request.GetType(), responseType),
            static key => CreateWrapper<RequestPipelineWrapper>(typeof(RequestPipelineWrapperImpl<,>), key));

        return wrapper.HandleUntyped(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    /// <inheritdoc />
    public IAsyncEnumerable<object?> StreamAsync(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var responseType = GetResponseType(request.GetType(), typeof(IKyrolusStreamRequest<>), "a stream request");
        var wrapper = s_streamWrappers.GetOrAdd(
            (request.GetType(), responseType),
            static key => CreateWrapper<StreamPipelineWrapper>(typeof(StreamPipelineWrapperImpl<,>), key));

        return wrapper.HandleUntyped(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    // --- Internals ---
    private Task<TResponse> ExecuteAsync<TResponse>(object request, CancellationToken cancellationToken)
    {
        var wrapper = (RequestPipelineWrapper<TResponse>)s_requestWrappers.GetOrAdd(
            (request.GetType(), typeof(TResponse)),
            static key => CreateWrapper<RequestPipelineWrapper>(typeof(RequestPipelineWrapperImpl<,>), key));

        return wrapper.Handle(request, _serviceProvider, _dispatcher, cancellationToken);
    }

    [UnconditionalSuppressMessage("AotAnalysis", "IL3050:RequiresDynamicCode",
        Justification = "Wrapper types are closed over request/response types that already exist in the closure of the caller's generic instantiation.")]
    private static TWrapper CreateWrapper<TWrapper>(Type openWrapperType, (Type RequestType, Type ResponseType) key)
        where TWrapper : class
    {
        var closedType = openWrapperType.MakeGenericType(key.RequestType, key.ResponseType);
        return (TWrapper)(Activator.CreateInstance(closedType)
            ?? throw new InvalidOperationException(
                $"[KyrolusMediator] Could not create a pipeline wrapper for {key.RequestType.FullName}."));
    }

    private static Type GetResponseType(Type requestType, Type openRequestInterface, string label)
    {
        return s_responseTypeCache.GetOrAdd((requestType, openRequestInterface), static key =>
        {
            var closed = Array.Find(
                key.RequestType.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == key.OpenInterface);

            return closed?.GetGenericArguments()[0]
                ?? throw new ArgumentException(
                    $"[KyrolusMediator] {key.RequestType.FullName} does not implement {key.OpenInterface.Name}.");
        });
    }
}
