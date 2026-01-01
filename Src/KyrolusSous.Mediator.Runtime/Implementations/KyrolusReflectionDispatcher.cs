namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Reflection-based dispatcher used when no generated dispatcher is registered.
/// </summary>
public sealed class KyrolusReflectionDispatcher : IGeneratedDispatcher
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handleMethodCache = new();

    public Task<TResponse> DispatchRequestAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sp);

        var requestType = request.GetType();
        var handlerInterfaceType = ResolveRequestHandlerInterface<TResponse>(request, requestType);
        var handler = sp.GetService(handlerInterfaceType);
        if (handler is null)
        {
            throw new InvalidOperationException($"[KyrolusMediator] No handler registered for {requestType.FullName} returning {typeof(TResponse).FullName}.");
        }

        var handleMethod = GetHandleMethod(handler.GetType(), requestType);
        var task = (Task<TResponse>)handleMethod.Invoke(handler, new object[] { request, ct })!;
        return task;
    }

    public Task DispatchCommandAsync(object command, IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(sp);

        var requestType = command.GetType();
        var handlerInterfaceType = typeof(IKyrolusCommandHandler<>).MakeGenericType(requestType);
        var handler = sp.GetService(handlerInterfaceType);
        if (handler is null && command is IKyrolusRequest<Unit>)
        {
            handlerInterfaceType = typeof(IKyrolusRequestHandler<>).MakeGenericType(requestType);
            handler = sp.GetService(handlerInterfaceType);
        }

        if (handler is null)
        {
            throw new InvalidOperationException($"[KyrolusMediator] No handler registered for command {requestType.FullName}.");
        }

        var handleMethod = GetHandleMethod(handler.GetType(), requestType);
        return (Task)handleMethod.Invoke(handler, new object[] { command, ct })!;
    }

    public IAsyncEnumerable<TResponse> DispatchStreamAsync<TResponse>(object request, IServiceProvider sp, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(sp);

        var requestType = request.GetType();
        var handlerInterfaceType = typeof(IKyrolusStreamRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        var handler = sp.GetService(handlerInterfaceType);
        if (handler is null)
        {
            throw new InvalidOperationException($"[KyrolusMediator] No stream handler registered for {requestType.FullName} producing {typeof(TResponse).FullName}.");
        }

        var handleMethod = GetHandleMethod(handler.GetType(), requestType);
        return (IAsyncEnumerable<TResponse>)handleMethod.Invoke(handler, new object[] { request, ct })!;
    }

    private static Type ResolveRequestHandlerInterface<TResponse>(object request, Type requestType)
    {
        if (request is IKyrolusCommandBase)
        {
            return typeof(IKyrolusCommandHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        }

        if (request is IKyrolusQueryBase)
        {
            return typeof(IKyrolusQueryHandler<,>).MakeGenericType(requestType, typeof(TResponse));
        }

        return typeof(IKyrolusRequestHandler<,>).MakeGenericType(requestType, typeof(TResponse));
    }

    private static MethodInfo GetHandleMethod(Type handlerType, Type requestType)
    {
        return s_handleMethodCache.GetOrAdd(handlerType, type =>
            type.GetMethod("Handle", new[] { requestType, typeof(CancellationToken) })
            ?? throw new InvalidOperationException($"[KyrolusMediator] Could not find Handle({requestType.Name}, CancellationToken) on {type.FullName}."));
    }
}
