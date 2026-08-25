using System.Diagnostics.CodeAnalysis;
using KyrolusSous.Mediator.Runtime.Internal;

namespace KyrolusSous.Mediator.Reflection;

/// <summary>
/// Closes the pipeline wrapper types on demand, for applications that do not use the generator.
/// </summary>
/// <remarks>
/// The generated source and this one implement the same interface, and the sender cannot tell them
/// apart. The difference is entirely in when the closed types come into existence: at compile time
/// there, at first use here.
/// </remarks>
[RequiresDynamicCode("Closes the pipeline wrapper types over the request and response types at runtime.")]
[RequiresUnreferencedCode("Reads the interfaces a request declares, which trimming may remove.")]
internal sealed class ReflectionPipelineWrapperSource : IKyrolusPipelineWrapperSource
{
    /// <summary>Caches the response type declared by a request type, for the untyped overloads.</summary>
    private static readonly ConcurrentDictionary<(Type RequestType, bool Stream), Type?> s_responseTypes = new();

    public object CreateRequestWrapper(Type requestType, Type responseType)
        => Create(typeof(RequestPipelineWrapperImpl<,>), requestType, responseType);

    public object CreateStreamWrapper(Type requestType, Type responseType)
        => Create(typeof(StreamPipelineWrapperImpl<,>), requestType, responseType);

    public Type? GetResponseType(Type requestType, bool stream)
        => s_responseTypes.GetOrAdd((requestType, stream), static key =>
        {
            var openInterface = key.Stream ? typeof(IKyrolusStreamRequest<>) : typeof(IKyrolusRequest<>);
            var closed = Array.FindAll(
                key.RequestType.GetInterfaces(),
                i => i.IsGenericType && i.GetGenericTypeDefinition() == openInterface);

            // Exactly one, or the answer would be a guess. A request declaring two responses is
            // legal and dispatches fine through the typed overloads; only the untyped ones, which
            // have nothing but the request to go on, cannot resolve it.
            return closed.Length == 1 ? closed[0].GetGenericArguments()[0] : null;
        });

    private static object Create(Type openWrapperType, Type requestType, Type responseType)
    {
        var closedType = openWrapperType.MakeGenericType(requestType, responseType);
        return CreateWrapperInstance(closedType, requestType);
    }

    [ExcludeFromCodeCoverage]
    private static object CreateWrapperInstance(Type closedType, Type requestType)
        => Activator.CreateInstance(closedType)
            ?? throw new InvalidOperationException($"[KyrolusMediator] Could not create a pipeline wrapper for {requestType.FullName}.");
}

/// <summary>
/// Resolves notification handlers by closing <c>INotificationHandler&lt;&gt;</c> at runtime.
/// </summary>
[RequiresDynamicCode("Closes INotificationHandler<> over the notification type at runtime.")]
[RequiresUnreferencedCode("Finds each handler's Handle method by name, which trimming cannot see.")]
internal sealed class ReflectionNotificationDispatchSource : IKyrolusNotificationDispatchSource
{
    // Key: (concrete handler type, notification type). Both parts are required - one handler class
    // may implement INotificationHandler<> for several notifications, and keying on the handler
    // alone would hand back the Handle overload of whichever notification arrived first.
    private static readonly ConcurrentDictionary<(Type HandlerType, Type NotificationType), MethodInfo> s_handleMethods = new();

    public IReadOnlyList<Func<CancellationToken, Task>> CreateHandlerInvocations(
        object notification,
        IServiceProvider serviceProvider)
    {
        var notificationType = notification.GetType();
        var handlerInterfaceType = typeof(INotificationHandler<>).MakeGenericType(notificationType);

        return
        [
            .. serviceProvider.GetServices(handlerInterfaceType)
                .Where(handler => handler is not null)
                .Select(handler => (Func<CancellationToken, Task>)(async ct =>
                {
                    var handle = s_handleMethods.GetOrAdd((handler!.GetType(), notificationType), static key =>
                        key.HandlerType.GetMethod("Handle", [key.NotificationType, typeof(CancellationToken)])
                        ?? throw new InvalidOperationException(
                            $"[KyrolusMediator] Could not find Handle({key.NotificationType.Name}, CancellationToken) on {key.HandlerType.FullName}."));

                    await InvokeAsync(handle, handler, [notification, ct]).ConfigureAwait(false);
                }))
        ];
    }

    /// <summary>
    /// Invokes a handler method and awaits the task it returns, reporting what the handler threw
    /// rather than the <see cref="TargetInvocationException"/> reflection wraps it in.
    /// </summary>
    internal static async Task InvokeAsync(MethodInfo method, object target, object?[] arguments)
    {
        Task? task;
        try
        {
            task = (Task?)method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException is not null)
        {
            throw Rethrow(exception.InnerException);
        }

        if (task is null)
        {
            throw new InvalidOperationException($"[KyrolusMediator] Handler '{method.DeclaringType?.FullName}' returned a null Task.");
        }

        await task.ConfigureAwait(false);
    }

    [ExcludeFromCodeCoverage]
    internal static Exception Rethrow(Exception innerException)
    {
        ExceptionDispatchInfo.Capture(innerException).Throw();
        return innerException;
    }
}

/// <summary>
/// Resolves exception actions and handlers by closing their interfaces at runtime.
/// </summary>
[RequiresDynamicCode("Closes the exception action and handler interfaces over the exception and response types.")]
[RequiresUnreferencedCode("Finds Execute and Handle by name, which trimming cannot see.")]
internal sealed class ReflectionRequestExceptionDispatchSource : IKyrolusRequestExceptionDispatchSource
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_actionExecuteMethods = new();
    private static readonly ConcurrentDictionary<Type, MethodInfo> s_handlerHandleMethods = new();

    public IReadOnlyList<(Type ActionType, Func<CancellationToken, Task> Invoke)> CreateActionInvocations(
        Type requestType,
        Type exceptionType,
        object request,
        Exception exception,
        IServiceProvider serviceProvider)
    {
        var serviceType = typeof(IKyrolusRequestExceptionAction<,>).MakeGenericType(requestType, exceptionType);
        var method = s_actionExecuteMethods.GetOrAdd(serviceType, static t => GetRequiredMethod(t, "Execute"));

        return
        [
            .. serviceProvider.GetServices(serviceType)
                .Where(action => action is not null)
                .Select(action => (
                    action!.GetType(),
                    (Func<CancellationToken, Task>)(ct =>
                        ReflectionNotificationDispatchSource.InvokeAsync(method, action, [request, exception, ct]))))
        ];
    }

    public IReadOnlyList<Func<CancellationToken, Task>> CreateHandlerInvocations(
        Type requestType,
        Type exceptionType,
        Type responseType,
        object request,
        Exception exception,
        object state,
        IServiceProvider serviceProvider)
    {
        var serviceType = typeof(IKyrolusRequestExceptionHandler<,,>).MakeGenericType(requestType, exceptionType, responseType);
        var method = s_handlerHandleMethods.GetOrAdd(serviceType, static t => GetRequiredMethod(t, "Handle"));

        return
        [
            .. serviceProvider.GetServices(serviceType)
                .Where(handler => handler is not null)
                .Select(handler => (Func<CancellationToken, Task>)(ct =>
                    ReflectionNotificationDispatchSource.InvokeAsync(method, handler!, [request, exception, state, ct])))
        ];
    }

    [ExcludeFromCodeCoverage]
    private static MethodInfo GetRequiredMethod(Type type, string name)
        => type.GetMethod(name) ?? throw new InvalidOperationException($"[KyrolusMediator] Could not find method {name} on {type.FullName}.");
}
