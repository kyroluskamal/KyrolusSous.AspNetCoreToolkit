using Microsoft.Extensions.Logging;

namespace KyrolusSous.Mediator.Runtime.Implementations;

/// <summary>
/// Outermost behavior: catches anything thrown by the rest of the pipeline, runs the registered
/// exception actions, then gives the exception handlers a chance to supply a replacement response.
/// </summary>
[PipelineOrder(-2000)]
public sealed class KyrolusRequestExceptionProcessorBehavior<TRequest, TResponse>(IServiceProvider serviceProvider)
    : IKyrolusPipelineBehavior<TRequest, TResponse>
{
    /// <summary>Stable logger category, rather than the mangled closed generic type name.</summary>
    private const string LoggerCategory = "KyrolusSous.Mediator.RequestExceptionProcessor";

    private readonly IServiceProvider _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));

    public async Task<TResponse> Handle(TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        try
        {
            return await next(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await ExecuteActionsAsync(request, ex, cancellationToken).ConfigureAwait(false);

            var state = new KyrolusRequestExceptionHandlerState<TResponse>();
            await ExecuteHandlersAsync(request, ex, state, cancellationToken).ConfigureAwait(false);
            if (state.Handled)
                return state.Response!;
            throw;
        }
    }

    /// <summary>
    /// Runs every registered action for the exception and each of its base types.
    /// </summary>
    /// <remarks>
    /// Each action is isolated. Actions are independent side effects - logging, metrics, alerting -
    /// so one of them failing must not skip the rest, and must never replace the exception the
    /// request actually failed with. An unreachable metrics backend hiding the real bug is a worse
    /// failure than the metric being missed.
    /// <para>
    /// A swallowed failure is still reported through <see cref="ILogger"/> when logging is
    /// registered, so this cannot hide a broken action forever.
    /// </para>
    /// </remarks>
    private async Task ExecuteActionsAsync(TRequest request, Exception exception, CancellationToken cancellationToken)
    {
        foreach (var exceptionType in GetExceptionTypes(exception))
        {
            var actionType = typeof(IKyrolusRequestExceptionAction<,>).MakeGenericType(typeof(TRequest), exceptionType);
            var method = actionType.GetMethod("Execute");
            if (method is null) continue;

            foreach (var action in _serviceProvider.GetServices(actionType))
            {
                if (action is null) continue;

                try
                {
                    var task = (Task)method.Invoke(action, [request, exception, cancellationToken])!;
                    await task.ConfigureAwait(false);
                }
                catch (Exception actionFailure)
                {
                    ReportActionFailure(action, exception, Unwrap(actionFailure));
                }
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
                if (method is null) continue;

                var task = (Task)method.Invoke(handler, [request, exception, state, cancellationToken])!;
                await task.ConfigureAwait(false);

                if (state.Handled) return;
            }
        }
    }

    /// <summary>
    /// Logs an action that threw, if logging is available. Deliberately best-effort: this runs
    /// while an exception is already in flight, so it must not throw a second one.
    /// </summary>
    private void ReportActionFailure(object action, Exception originalException, Exception actionFailure)
    {
        try
        {
            _serviceProvider.GetService<ILoggerFactory>()
                ?.CreateLogger(LoggerCategory)
                .LogError(
                    actionFailure,
                    "[KyrolusMediator] Exception action {Action} failed while handling {OriginalException} from {Request}. The original exception is unaffected.",
                    action.GetType().FullName,
                    originalException.GetType().FullName,
                    typeof(TRequest).FullName);
        }
        catch
        {
            // Logging itself is broken. Nothing sensible is left to do, and throwing here would
            // destroy the very exception this behavior exists to preserve.
        }
    }

    /// <summary>Reflection wraps handler exceptions; report the one the action actually threw.</summary>
    private static Exception Unwrap(Exception exception)
        => exception is TargetInvocationException { InnerException: { } inner } ? inner : exception;

    /// <summary>
    /// Yields the exception's type and every base type up to <see cref="Exception"/>, most specific
    /// first - so a handler registered for a precise type runs before a general one.
    /// </summary>
    private static IEnumerable<Type> GetExceptionTypes(Exception exception)
    {
        for (Type? current = exception.GetType();
             current is not null && typeof(Exception).IsAssignableFrom(current);
             current = current.BaseType)
            yield return current;
    }
    
}
