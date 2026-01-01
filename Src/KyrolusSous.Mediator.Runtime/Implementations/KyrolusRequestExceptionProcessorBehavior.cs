namespace KyrolusSous.Mediator.Runtime.Implementations;

[PipelineOrder(-2000)]
public sealed class KyrolusRequestExceptionProcessorBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ExecuteActionsAsync(request, ex, cancellationToken).ConfigureAwait(false);

            var state = new KyrolusRequestExceptionHandlerState<TResponse>();
            await ExecuteHandlersAsync(request, ex, state, cancellationToken).ConfigureAwait(false);

            if (state.Handled)
            {
                return state.Response!;
            }

            throw;
        }
    }

    private async Task ExecuteActionsAsync(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        foreach (var exceptionType in GetExceptionTypes(exception))
        {
            var actionType = typeof(IKyrolusRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
            var actions = _serviceProvider.GetServices(actionType);
            foreach (var action in actions)
            {
                var method = actionType.GetMethod("Execute");
                if (method is null)
                {
                    continue;
                }

                var task = (Task)method.Invoke(action, [request, exception, cancellationToken])!;
                await task.ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteHandlersAsync(TRequest request,
        Exception exception,
        KyrolusRequestExceptionHandlerState<TResponse> state,
        CancellationToken cancellationToken)
    {
        foreach (var exceptionType in GetExceptionTypes(exception))
        {
            var handlerType = typeof(IKyrolusRequestExceptionHandler<,,>).MakeGenericType(
                typeof(TRequest),
                exceptionType,
                typeof(TResponse));
            var handlers = _serviceProvider.GetServices(handlerType);
            foreach (var handler in handlers)
            {
                var method = handlerType.GetMethod("Handle");
                if (method is null)
                {
                    continue;
                }

                var task = (Task)method.Invoke(handler, [request, exception, state, cancellationToken])!;
                await task.ConfigureAwait(false);

                if (state.Handled)
                {
                    return;
                }
            }
        }
    }

    private static IEnumerable<Type> GetExceptionTypes(Exception exception)
    {
        for (Type? current = exception.GetType(); current is not null; current = current.BaseType)
        {
            yield return current;
        }
    }
}
