using System.Diagnostics.CodeAnalysis;

namespace KyrolusSous.Mediator.Reflection;

/// <summary>
/// Reflection-based dispatcher used when no generated dispatcher is registered.
/// </summary>
/// <remarks>
/// This is the whole reason the generator exists. Every dispatch here closes a handler interface
/// with <c>MakeGenericType</c> and calls <c>Handle</c> through a <see cref="MethodInfo"/>, neither
/// of which an ahead-of-time published application can do. The annotations are on the type rather
/// than the individual members because there is no safe subset: reaching this class at all means
/// the generated dispatcher did not replace it.
/// </remarks>
[RequiresDynamicCode(
    "Closes the handler interfaces over the request and response types at runtime. Reference " +
    "KyrolusSous.Mediator.Generator, which emits a dispatch table and replaces this dispatcher.")]
[RequiresUnreferencedCode(
    "Finds each handler's Handle method by name, which trimming cannot see. Reference " +
    "KyrolusSous.Mediator.Generator, which emits direct calls instead.")]
public sealed class KyrolusReflectionDispatcher : IMediatorDispatcher
{
    // Key: (concrete handler type, request type). The request type is part of the key because one
    // handler class may implement IKyrolusRequestHandler<> for several requests - keying on the
    // handler alone would return the Handle overload of whichever request was dispatched first.
    private static readonly ConcurrentDictionary<(Type HandlerType, Type RequestType), MethodInfo> s_handleMethodCache = new();

    public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sp);

        var requestType = request.GetType();
        var handlerInterfaceType = ResolveRequestHandlerInterface<TResponse>(request, requestType);
        var handler = sp.GetService(handlerInterfaceType) ?? throw new InvalidOperationException($"[KyrolusMediator] No handler registered for {requestType.FullName} returning {typeof(TResponse).FullName}.");

        var handleMethod = GetHandleMethod(handler.GetType(), requestType);
        return Invoke<Task<TResponse>>(handleMethod, handler, request, ct);
    }

    public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(sp);

        var requestType = command.GetType();
        Type? handlerInterfaceType;
        object? handler = null;

        if (command is IKyrolusCommand)
        {
            handlerInterfaceType = typeof(IKyrolusCommandHandler<>).MakeGenericType(requestType);
            handler = sp.GetService(handlerInterfaceType);
        }

        if (handler is null && command is IKyrolusRequest<Unit>)
        {
            handlerInterfaceType = typeof(IKyrolusRequestHandler<>).MakeGenericType(requestType);
            handler = sp.GetService(handlerInterfaceType);
        }

        if (handler is null)
            throw new InvalidOperationException($"[KyrolusMediator] No handler registered for command {requestType.FullName}.");

        var handleMethod = GetHandleMethod(handler.GetType(), requestType);
        return Invoke<Task>(handleMethod, handler, command, ct);
    }

    public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sp);

        var requestType = request.GetType();
        var handlerInterfaceType = typeof(IKyrolusStreamRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = sp.GetService(handlerInterfaceType) ?? throw new InvalidOperationException($"[KyrolusMediator] No stream handler registered for {requestType.FullName} producing {typeof(TResponse).FullName}.");

        var handleMethod = GetHandleMethod(handler.GetType(), requestType);
        return Invoke<IAsyncEnumerable<TResponse>>(handleMethod, handler, request, ct);
    }

    private static Type ResolveRequestHandlerInterface<TResponse>(object request, Type requestType)
    {
        if (request is IKyrolusCommandBase)
            return typeof(IKyrolusCommandHandler<,>).MakeGenericType(requestType, typeof(TResponse));
            
        if (request is IKyrolusQueryBase)
            return typeof(IKyrolusQueryHandler<,>).MakeGenericType(requestType, typeof(TResponse));

        return typeof(IKyrolusRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
    }

    private static MethodInfo GetHandleMethod(Type handlerType, Type requestType)
    {
        return s_handleMethodCache.GetOrAdd((handlerType, requestType), static key =>
            key.HandlerType.GetMethod("Handle", [key.RequestType, typeof(CancellationToken)])
            ?? throw new InvalidOperationException($"[KyrolusMediator] Could not find Handle({key.RequestType.Name}, CancellationToken) on {key.HandlerType.FullName}."));
    }

    /// <summary>
    /// Invokes the handler's <c>Handle</c> method and casts the result to <typeparamref name="TResult"/>.
    /// </summary>
    /// <remarks>
    /// One method covers all three dispatch shapes - <c>Task&lt;TResponse&gt;</c>, bare <c>Task</c>
    /// and <c>IAsyncEnumerable&lt;TResponse&gt;</c> - because only the cast differs.
    /// <para>
    /// The catch is the reason this is worth centralising. Reflection wraps anything the handler
    /// throws in a <see cref="TargetInvocationException"/>, so without unwrapping it a caller
    /// catching <c>ValidationException</c> would never see one. Rethrowing through
    /// <see cref="ExceptionDispatchInfo"/> rather than <c>throw exception.InnerException</c>
    /// preserves the original stack trace instead of resetting it to this line.
    /// </para>
    /// </remarks>
    private static TResult Invoke<TResult>(MethodInfo handleMethod, object handler, object request, CancellationToken cancellationToken)
    {
        try
        {
            return (TResult)handleMethod.Invoke(handler, [request, cancellationToken])!;
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }
}
